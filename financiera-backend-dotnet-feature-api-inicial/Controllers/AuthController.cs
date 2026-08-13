using ApiEjemplo.Data;
using ApiEjemplo.DTOs;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using ApiEjemplo.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ==========================
        // POST: api/auth/login
        // ==========================
        // MONEYPINE-MT: unico endpoint junto con Refresh que debe quedar publico.
        // El FallbackPolicy en Program.cs exige autenticacion por defecto; sin este
        // atributo nadie podria loguearse (no hay JWT antes de tener JWT).
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            // Buscar usuario por correo
            // MONEYPINE-MT: IgnoreQueryFilters() deliberado — I4 lo prohíbe fuera de
            // api/platform/*, pero login es el ÚNICO caso legítimo: en este punto no
            // hay JWT, TenantResolutionMiddleware no corrió, e ITenantContext está en
            // su default (PrestamistaId=0). Con el filtro activo esta consulta jamás
            // encuentra al usuario real (prestamista_id=1) — el login queda roto para
            // TODOS los tenants. El tenant recién se conoce leyendo usuario.prestamista_id
            // aquí mismo, y de ahí sale el claim del JWT (ver GenerarAccessToken). Riesgo
            // conocido: correo no es único globalmente (ver deuda del arquitecto), así
            // que un correo duplicado entre dos tenants es ambiguo — deuda existente,
            // no introducida por este cambio.
            var usuario = await _context.Usuarios
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.correo == request.correo);

            // MONEYPINE-FIX: se responde con objeto JSON, no con string plano.
            // Unauthorized("texto") sale como text/plain y el cliente, que hace
            // response.json(), no podia leerlo: al usuario le aparecia "Error 401"
            // en vez del motivo real. Mismo criterio en el resto del metodo.
            //
            // El mensaje es DELIBERADAMENTE igual para "correo no existe" y
            // "password incorrecta": distinguirlos permitiria enumerar que
            // correos estan dados de alta en el sistema.
            if (usuario == null)
                return Unauthorized(new { message = "Usuario o contraseña incorrectos" });

            // Verificar contraseña — soporta BCrypt (migrado) y ASP.NET Identity (nuevos)
            bool passwordValida;
            if (usuario.password_hash.StartsWith("$2a$") || usuario.password_hash.StartsWith("$2b$"))
            {
                // MONEYPINE-FIX: hashes BCrypt del sistema anterior
                passwordValida = BCrypt.Net.BCrypt.Verify(request.password, usuario.password_hash);
            }
            else
            {
                var hasher = new PasswordHasher<Usuario>();
                var resultado = hasher.VerifyHashedPassword(usuario, usuario.password_hash, request.password);
                passwordValida = resultado != PasswordVerificationResult.Failed;
            }

            if (!passwordValida)
                return Unauthorized(new { message = "Usuario o contraseña incorrectos" });

            // Verificar estado
            if (usuario.estado == EstadoUsuario.INACTIVO)
                return StatusCode(403, new { message = "Tu cuenta está inactiva. Contacta al administrador" });

            if (usuario.estado == EstadoUsuario.BLOQUEADO)
                return StatusCode(403, new { message = "Tu cuenta está bloqueada. Contacta al administrador" });

            // MONEYPINE-MT: Fase 3 — un tenant SUSPENDIDO o CANCELADO no puede
            // loguear a ninguno de sus usuarios. PLATFORM_ADMIN no pertenece a
            // la cartera de ningún tenant (su prestamista_id es solo el que le
            // tocó al crear la fila), así que se excluye de este chequeo.
            if (usuario.rol != RolUsuario.PLATFORM_ADMIN)
            {
                var prestamista = await _context.Prestamistas
                    .FirstOrDefaultAsync(p => p.prestamista_id == usuario.prestamista_id);
                if (prestamista != null && prestamista.estatus != EstatusPrestamista.ACTIVO)
                    return StatusCode(403, new { message = "Tu organización está suspendida" });
            }

            // Obtener permisos
            var permisos = PermisosPorRol.Obtener(usuario.rol);

            // Generar tokens
            var accessToken = GenerarAccessToken(usuario, permisos);
            var refreshToken = GenerarRefreshTokenSeguro();

            // Hashear refresh token antes de guardarlo
            var refreshTokenHash = HashToken(refreshToken);

            var refreshTokenEntity = new RefreshToken
            {
                UsuarioId = usuario.usuario_id,
                Token = refreshTokenHash, // ← Guardamos el HASH
                ExpirationDate = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken, // ← Se envía el ORIGINAL al frontend
                expires_in = 300,
                user = new
                {
                    id = usuario.usuario_id,
                    name = $"{usuario.nombre} {usuario.apellido}",
                    role = usuario.rol.ToString(),
                    correo = usuario.correo,
                    permissions = permisos
                }
            });
        }

        // ==========================
        // POST: api/auth/refresh
        // ==========================
        // MONEYPINE-MT: publico por la misma razon que Login — el cliente aun no
        // tiene access token vigente cuando llama a este endpoint.
        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(TokenRefreshRequestDto request)
        {
            // Hashear el token recibido
            var hashedToken = HashToken(request.RefreshToken);

            // Buscar en BD validando:
            // - Que exista
            // - Que no esté revocado
            // - Que NO esté expirado
            // MONEYPINE-MT: IgnoreQueryFilters() deliberado — mismo motivo que en Login.
            // RefreshToken no es ITenantEntity, pero el Include(Usuario) trae el filtro
            // de Usuario como predicado del JOIN; sin JWT (AllowAnonymous) el tenant está
            // en su default y el JOIN nunca encontraría al usuario real, dejando /refresh
            // roto para todos los tenants (EF además advierte de esto al construir el
            // modelo: "required end of a relationship... filtered out").
            var storedToken = await _context.RefreshTokens
                .IgnoreQueryFilters()
                .Include(rt => rt.Usuario)
                .FirstOrDefaultAsync(rt =>
                    rt.Token == hashedToken &&
                    !rt.IsRevoked &&
                    rt.ExpirationDate > DateTime.UtcNow
                );

            if (storedToken == null)
                return Unauthorized(new { message = "Refresh token inválido o expirado" });

            var usuario = storedToken.Usuario;

            // Obtener permisos
            var permisos = PermisosPorRol.Obtener(usuario.rol);

            // Generar nuevo access token
            var newAccessToken = GenerarAccessToken(usuario, permisos);

            return Ok(new
            {
                access_token = newAccessToken,
                expires_in = 300
            });
        }

        // ==========================
        // GET: api/auth/me
        // Devuelve el usuario autenticado con su rol leído de la BD
        // (no del token ni de localStorage) — fuente de verdad para el frontend.
        // ==========================
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var usuarioId))
                return Unauthorized(new { message = "Token sin identificador de usuario" });

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return Unauthorized(new { message = "Usuario no encontrado" });

            if (usuario.estado != EstadoUsuario.ACTIVO)
                return StatusCode(403, new { message = "Cuenta inactiva o bloqueada" });

            return Ok(new
            {
                usuario_id = usuario.usuario_id,
                correo     = usuario.correo,
                nombre     = usuario.nombre,
                apellido   = usuario.apellido,
                rol        = usuario.rol.ToString(),
                estado     = usuario.estado.ToString(),
            });
        }

        // ==========================
        // ACCESS TOKEN (5 minutos)
        // ==========================
        private string GenerarAccessToken(Usuario usuario, List<string> permisos)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.usuario_id.ToString()),
                new Claim(ClaimTypes.Email, usuario.correo),
                new Claim(ClaimTypes.Role, usuario.rol.ToString()),
                // MONEYPINE-MT: TenantResolutionMiddleware lee este claim para resolver
                // ITenantContext en cada request. Sin él, cualquier usuario autenticado
                // no-plataforma recibe 403 "Token sin tenant asignado".
                new Claim("prestamista_id", usuario.prestamista_id.ToString())
            };

            claims.AddRange(permisos.Select(p => new Claim("permission", p)));

            return CrearToken(claims, 5);
        }

        // ==========================
        // REFRESH TOKEN (7 días)
        // ==========================
        private string GenerarRefreshTokenSeguro()
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        // ==========================
        // HASH DEL REFRESH TOKEN
        // ==========================
        private string HashToken(string token)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        // ==========================
        // MÉTODO BASE PARA JWT
        // ==========================
        private string CrearToken(IEnumerable<Claim> claims, int minutos)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
            );

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(minutos),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}