using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using ApiEjemplo.DTOs.Cliente;
using ApiEjemplo.Services;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClienteController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ActivityService _activityService;

        public ClienteController(AppDbContext context, NotificationService notificationService, ActivityService activityService)
        {
            _context = context;
            _notificationService = notificationService;
            _activityService = activityService;
        }

        // ============================
        // GET: api/Cliente
        // Obtiene TODOS los clientes
        // ============================
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var clientes = await _context.Clientes
                .Include(c => c.Usuario)
                .Select(c => new ClienteDTO
                {
                    cliente_id         = c.cliente_id,
                    clave_cliente      = c.clave_cliente ?? $"MP-{c.cliente_id.ToString().PadLeft(5, '0')}",
                    usuario_id         = c.usuario_id,
                    tipo_cliente       = c.tipo_cliente,
                    ruta_vinculacion   = c.ruta_vinculacion,
                    permitir_acceso_web= c.permitir_acceso_web,
                    nombre_usuario     = c.Usuario != null ? c.Usuario.nombre    : null,
                    apellido_usuario   = c.Usuario != null ? c.Usuario.apellido  : null,
                    apellido_materno   = c.apellido_materno,
                    correo_usuario     = c.Usuario != null ? c.Usuario.correo    : null,
                    telefono_usuario   = c.Usuario != null ? c.Usuario.telefono  : null,
                    estado_usuario     = c.Usuario != null ? c.Usuario.estado.ToString() : null, // MONEYPINE-FIX: exponer estado del usuario
                    sexo               = c.sexo,
                    estado_civil       = c.estado_civil,
                    lugar_nacimiento   = c.lugar_nacimiento,
                    no_dependientes    = c.no_dependientes,
                    telefono_oficina   = c.telefono_oficina,
                    telefono_particular= c.telefono_particular,
                    direccion          = c.direccion,
                    colonia            = c.colonia,
                    fecha_nacimiento   = c.fecha_nacimiento,
                    curp               = c.curp,
                    rfc                = c.rfc,
                    empresa_nombre     = c.empresa_nombre,
                    empresa_rfc        = c.empresa_rfc,
                    empresa_correo     = c.empresa_correo,
                    empresa_telefono_oficina    = c.empresa_telefono_oficina,
                    empresa_telefono_particular = c.empresa_telefono_particular,
                    empresa_telefono_celular    = c.empresa_telefono_celular,
                })
                .ToListAsync();

            return Ok(clientes);
        }

        // ============================
        // GET: api/Cliente/5
        // Obtiene un cliente por ID
        // ============================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Usuario)
                .Include(c => c.Prestamos)
                .FirstOrDefaultAsync(c => c.cliente_id == id);

            if (cliente == null)
                return NotFound("Cliente no encontrado");

            return Ok(cliente);
        }

        // ============================
        // POST: api/Cliente
        // Crea un nuevo cliente
        // ============================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClienteCreateDTO dto)
        {
            var usuarioExiste = await _context.Usuarios
                .AnyAsync(u => u.usuario_id == dto.usuario_id);

            if (!usuarioExiste)
                return BadRequest("El usuario asociado no existe");

            var cliente = new Cliente
            {
                usuario_id = dto.usuario_id,
                direccion = dto.direccion,
                fecha_nacimiento = dto.fecha_nacimiento,
                curp = dto.curp,
                rfc = dto.rfc
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            cliente.clave_cliente = $"MP-{cliente.cliente_id.ToString().PadLeft(5, '0')}";
            await _context.SaveChangesAsync();

            var usuario = await _context.Usuarios.FindAsync(dto.usuario_id);
            var nombre = usuario != null ? $"{usuario.nombre} {usuario.apellido}" : $"Cliente #{cliente.cliente_id}";

            await _notificationService.CreateNotification(
                1,
                $"Nuevo cliente registrado: {nombre}",
                NotificationLevel.POSITIVE
            );
            await _activityService.CreateActivity(
                ActivityType.CUSTOM,
                cliente.cliente_id,
                0,
                NotificationLevel.POSITIVE
            );

            return Ok(cliente);
        }

        // ============================
        // PUT: api/Cliente/5
        // Actualiza un cliente existente
        // ============================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClienteUpdateDTO dto)
        {
            if (id != dto.cliente_id)
                return BadRequest("El ID no coincide");

            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null)
                return NotFound("Cliente no encontrado");

            cliente.usuario_id = dto.usuario_id;
            cliente.tipo_cliente = dto.tipo_cliente ?? cliente.tipo_cliente;
            cliente.ruta_vinculacion = dto.ruta_vinculacion ?? cliente.ruta_vinculacion;
            if (dto.permitir_acceso_web.HasValue)
                cliente.permitir_acceso_web = dto.permitir_acceso_web.Value;

            cliente.apellido_materno = dto.apellido_materno ?? cliente.apellido_materno;
            cliente.sexo = dto.sexo ?? cliente.sexo;
            cliente.estado_civil = dto.estado_civil ?? cliente.estado_civil;
            cliente.lugar_nacimiento = dto.lugar_nacimiento ?? cliente.lugar_nacimiento;
            if (dto.no_dependientes.HasValue)
                cliente.no_dependientes = dto.no_dependientes;
            cliente.telefono_oficina = dto.telefono_oficina ?? cliente.telefono_oficina;
            cliente.telefono_particular = dto.telefono_particular ?? cliente.telefono_particular;

            cliente.direccion        = dto.direccion        ?? cliente.direccion;
            cliente.colonia          = dto.colonia          ?? cliente.colonia;
            cliente.cp               = dto.cp               ?? cliente.cp;
            cliente.estado_domicilio = dto.estado_domicilio ?? cliente.estado_domicilio;
            cliente.municipio        = dto.municipio        ?? cliente.municipio;
            cliente.num_ext          = dto.num_ext          ?? cliente.num_ext;
            cliente.fecha_nacimiento = dto.fecha_nacimiento ?? cliente.fecha_nacimiento;
            cliente.curp = dto.curp ?? cliente.curp;
            cliente.rfc = dto.rfc ?? cliente.rfc;

            cliente.empresa_nombre = dto.empresa_nombre ?? cliente.empresa_nombre;
            cliente.empresa_rfc = dto.empresa_rfc ?? cliente.empresa_rfc;
            cliente.empresa_correo = dto.empresa_correo ?? cliente.empresa_correo;
            cliente.empresa_telefono_oficina = dto.empresa_telefono_oficina ?? cliente.empresa_telefono_oficina;
            cliente.empresa_telefono_particular = dto.empresa_telefono_particular ?? cliente.empresa_telefono_particular;
            cliente.empresa_telefono_celular = dto.empresa_telefono_celular ?? cliente.empresa_telefono_celular;

            // Actualizar datos del usuario asociado
            if (dto.nombre != null || dto.apellido != null || dto.correo != null || dto.telefono != null)
            {
                var usuario = await _context.Usuarios.FindAsync(cliente.usuario_id);
                if (usuario != null)
                {
                    if (dto.nombre != null) usuario.nombre = dto.nombre;
                    if (dto.apellido != null) usuario.apellido = dto.apellido;
                    if (dto.correo != null) usuario.correo = dto.correo;
                    if (dto.telefono != null) usuario.telefono = dto.telefono;
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ============================
        // POST: api/Cliente/backfill-claves
        // Genera claves MP-XXXXX para clientes que no la tengan
        // ============================
        [HttpPost("backfill-claves")]
        public async Task<IActionResult> BackfillClaves()
        {
            var sinClave = await _context.Clientes
                .Where(c => c.clave_cliente == null || c.clave_cliente == "")
                .ToListAsync();

            foreach (var c in sinClave)
                c.clave_cliente = $"MP-{c.cliente_id.ToString().PadLeft(5, '0')}";

            await _context.SaveChangesAsync();
            return Ok(new { actualizados = sinClave.Count });
        }

        // ============================
        // DELETE: api/Cliente/5
        // Elimina un cliente
        // ============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null)
                return NotFound("Cliente no encontrado");

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                1,
                $"Cliente #{id} eliminado del sistema",
                NotificationLevel.NEUTRAL
            );

            return NoContent();
        }
    }
}