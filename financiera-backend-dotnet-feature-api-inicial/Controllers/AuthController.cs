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
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            // Buscar usuario por correo
            var usuario = await _context.Usuarios
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
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(TokenRefreshRequestDto request)
        {
            // Hashear el token recibido
            var hashedToken = HashToken(request.RefreshToken);

            // Buscar en BD validando:
            // - Que exista
            // - Que no esté revocado
            // - Que NO esté expirado
            var storedToken = await _context.RefreshTokens
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
                new Claim(ClaimTypes.Role, usuario.rol.ToString())
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