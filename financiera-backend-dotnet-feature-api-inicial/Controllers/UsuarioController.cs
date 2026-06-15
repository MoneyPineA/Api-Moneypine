using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ApiEjemplo.Helpers;
using ApiEjemplo.Enums;
using ApiEjemplo.DTOs.Usuario;
using ApiEjemplo.Services;
using System.Security.Claims;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuarioController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public UsuarioController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private int GetCallerId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private RolUsuario GetCallerRole() =>
            Enum.Parse<RolUsuario>(User.FindFirstValue(ClaimTypes.Role)!);

        // Nunca incluye password_hash
        private static UsuarioResponseDto MapToDto(Usuario u) => new()
        {
            usuario_id     = u.usuario_id,
            correo         = u.correo,
            nombre         = u.nombre,
            apellido       = u.apellido,
            telefono       = u.telefono,
            rol            = u.rol.ToString(),
            estado         = u.estado.ToString(),
            fecha_registro = u.fecha_registro
        };

        // ── GET: api/Usuario ─────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetUsuarios()
        {
            var rol = GetCallerRole();
            if (rol != RolUsuario.ADMIN && rol != RolUsuario.RECURSOS_HUMANOS)
                return Forbid();

            var usuarios = await _context.Usuarios.ToListAsync();
            return Ok(usuarios.Select(MapToDto));
        }

        // ── GET: api/Usuario/{id} ────────────────────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUsuario(int id)
        {
            var rol = GetCallerRole();
            if (rol != RolUsuario.ADMIN && rol != RolUsuario.RECURSOS_HUMANOS)
                return Forbid();

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            return Ok(MapToDto(usuario));
        }

        // ── POST: api/Usuario ────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CrearUsuario([FromBody] UsuarioCreateDto dto)
        {
            var rol = GetCallerRole();
            if (rol != RolUsuario.ADMIN && rol != RolUsuario.RECURSOS_HUMANOS)
                return Forbid();

            if (rol == RolUsuario.RECURSOS_HUMANOS && dto.rol == RolUsuario.ADMIN)
                return StatusCode(403, new { mensaje = "Recursos Humanos no puede crear cuentas con rol ADMIN." });

            if (await _context.Usuarios.AnyAsync(u => u.correo == dto.correo))
                return Conflict(new { mensaje = "Ya existe un usuario con ese correo." });

            var usuario = new Usuario
            {
                correo         = dto.correo,
                nombre         = dto.nombre,
                apellido       = dto.apellido,
                telefono       = dto.telefono,
                rol            = dto.rol,
                estado         = EstadoUsuario.ACTIVO,
                fecha_registro = TimeHelper.GetMexicoTime()
            };

            var hasher = new PasswordHasher<Usuario>();
            usuario.password_hash = hasher.HashPassword(usuario, dto.password);

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            if (usuario.rol == RolUsuario.CLIENTE)
            {
                await _notificationService.CreateNotification(
                    1,
                    $"Nuevo cliente registrado: {usuario.nombre} {usuario.apellido}",
                    NotificationLevel.POSITIVE
                );
            }

            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.usuario_id }, MapToDto(usuario));
        }

        // ── PUT: api/Usuario/{id} ────────────────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUsuario(int id, [FromBody] UsuarioUpdateDto dto)
        {
            var callerRol = GetCallerRole();
            var callerId  = GetCallerId();

            if (callerRol != RolUsuario.ADMIN && callerRol != RolUsuario.RECURSOS_HUMANOS)
                return Forbid();

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            // RECURSOS_HUMANOS: no puede tocar cuentas ADMIN ni asignar rol ADMIN
            if (callerRol == RolUsuario.RECURSOS_HUMANOS)
            {
                if (usuario.rol == RolUsuario.ADMIN)
                    return StatusCode(403, new { mensaje = "Recursos Humanos no puede modificar cuentas ADMIN." });

                if (dto.rol == RolUsuario.ADMIN)
                    return StatusCode(403, new { mensaje = "Recursos Humanos no puede asignar rol ADMIN." });
            }

            // Bloquear autopausarse
            bool intentaPausar = dto.estado != EstadoUsuario.ACTIVO && usuario.estado == EstadoUsuario.ACTIVO;
            if (callerId == id && intentaPausar)
                return BadRequest(new { mensaje = "No puedes pausar tu propia cuenta." });

            // Protección del último ADMIN activo
            if (usuario.rol == RolUsuario.ADMIN)
            {
                bool degrada = dto.rol != RolUsuario.ADMIN;
                bool pausa   = intentaPausar;

                if (degrada || pausa)
                {
                    int otrosAdminsActivos = await _context.Usuarios.CountAsync(u =>
                        u.rol    == RolUsuario.ADMIN &&
                        u.estado == EstadoUsuario.ACTIVO &&
                        u.usuario_id != id);

                    if (otrosAdminsActivos == 0)
                        return BadRequest(new { mensaje = "No puedes degradar o pausar al último ADMIN activo." });
                }
            }

            // Correo único (excluyendo al propio usuario)
            if (await _context.Usuarios.AnyAsync(u => u.correo == dto.correo && u.usuario_id != id))
                return Conflict(new { mensaje = "Ya existe otro usuario con ese correo." });

            usuario.correo   = dto.correo;
            usuario.nombre   = dto.nombre;
            usuario.apellido = dto.apellido;
            usuario.telefono = dto.telefono;
            usuario.rol      = dto.rol;
            usuario.estado   = dto.estado;

            if (!string.IsNullOrWhiteSpace(dto.nuevaPassword))
            {
                var hasher = new PasswordHasher<Usuario>();
                usuario.password_hash = hasher.HashPassword(usuario, dto.nuevaPassword);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── DELETE: api/Usuario/{id} ─────────────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var callerRol = GetCallerRole();
            var callerId  = GetCallerId();

            if (callerRol != RolUsuario.ADMIN)
                return Forbid();

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            if (callerId == id)
                return BadRequest(new { mensaje = "No puedes eliminar tu propia cuenta." });

            if (usuario.rol == RolUsuario.ADMIN)
            {
                int otrosAdminsActivos = await _context.Usuarios.CountAsync(u =>
                    u.rol    == RolUsuario.ADMIN &&
                    u.estado == EstadoUsuario.ACTIVO &&
                    u.usuario_id != id);

                if (otrosAdminsActivos == 0)
                    return BadRequest(new { mensaje = "No puedes eliminar al último ADMIN activo." });
            }

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
