using Microsoft.AspNetCore.Mvc;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ApiEjemplo.Helpers;
using ApiEjemplo.Enums;
using ApiEjemplo.DTOs.Usuario;
using ApiEjemplo.Services;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public UsuarioController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ============================
        // MAPPER: Usuario -> DTO
        // ============================
        private UsuarioResponseDto MapToDto(Usuario u)
        {
            return new UsuarioResponseDto
            {
                usuario_id = u.usuario_id,
                correo = u.correo,
                nombre = u.nombre,
                apellido = u.apellido,
                telefono = u.telefono,
                rol = u.rol.ToString(),
                estado = u.estado.ToString(),
                fecha_registro = u.fecha_registro
            };
        }

        // ============================
        // GET: api/Usuario
        // Obtiene TODOS los Usuarios
        // ============================
        [HttpGet]
        public async Task<IActionResult> GetUsuarios()
        {
            var usuarios = await _context.Usuarios.ToListAsync();

            var response = usuarios.Select(u => MapToDto(u));

            return Ok(response);
        }

        // ============================
        // GET: api/Usuario/5
        // Obtiene un Usuario por ID
        // ============================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            return Ok(MapToDto(usuario));
        }

        // ============================
        // POST: api/Usuario
        // Crea un nuevo Usuario
        // ============================
        [HttpPost]
        public async Task<IActionResult> CrearUsuario([FromBody] UsuarioCreateDto dto)
        {
            var usuario = new Usuario
            {
                correo = dto.correo,
                nombre = dto.nombre,
                apellido = dto.apellido,
                telefono = dto.telefono,
                rol = dto.rol,
                estado = EstadoUsuario.ACTIVO,
                fecha_registro = TimeHelper.GetMexicoTime()
            };

            // HASHEAR CONTRASEÑA
            var passwordHasher = new PasswordHasher<Usuario>();
            usuario.password_hash = passwordHasher.HashPassword(usuario, dto.password);

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

            return CreatedAtAction(
                nameof(GetUsuario),
                new { id = usuario.usuario_id },
                MapToDto(usuario)
            );
        }

        // ============================
        // PUT: api/Usuario/5
        // Actualiza un Usuario existente
        // ============================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUsuario(int id, [FromBody] UsuarioUpdateDto dto)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            usuario.correo = dto.correo;
            usuario.nombre = dto.nombre;
            usuario.apellido = dto.apellido;
            usuario.telefono = dto.telefono;
            usuario.rol = dto.rol;
            usuario.estado = dto.estado;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ============================
        // DELETE: api/Usuario/5
        // Elimina un Usuario
        // ============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado" });

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}