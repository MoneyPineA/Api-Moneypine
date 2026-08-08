using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Security;
using ApiEjemplo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ApiEjemplo.Controllers
{
    public class CrearSolicitudDto
    {
        public string tipo { get; set; } = string.Empty;
        public int entidad_id { get; set; }
        public string justificacion { get; set; } = string.Empty;
    }

    public class ResolverSolicitudDto
    {
        public string? respuesta { get; set; }
    }

    /// <summary>
    /// Bandeja de peticiones que los roles no-ADMIN hacen para ejecutar
    /// acciones sensibles, y su resolucion por parte de un ADMIN.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SolicitudAprobacionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SolicitudAprobacionService _service;

        public SolicitudAprobacionController(AppDbContext context, SolicitudAprobacionService service)
        {
            _context = context;
            _service = service;
        }

        private int UsuarioActual()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(idClaim, out var id);
            return id;
        }

        // =====================================================
        // POST: api/SolicitudAprobacion
        // Crea una peticion. La accion NO se ejecuta aqui.
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CrearSolicitudDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.justificacion))
                return BadRequest(new { message = "La justificación es obligatoria: el administrador necesita el contexto para decidir." });

            if (dto.justificacion.Trim().Length < 10)
                return BadRequest(new { message = "Explica el motivo con un poco más de detalle (mínimo 10 caracteres)." });

            if (!Enum.TryParse<TipoSolicitud>(dto.tipo, ignoreCase: true, out var tipo))
                return BadRequest(new { message = $"Tipo de solicitud desconocido: '{dto.tipo}'." });

            var solicitanteId = UsuarioActual();

            // Un ADMIN no necesita pedirse permiso a si mismo.
            if (User.IsInRole(nameof(RolUsuario.ADMIN)))
                return BadRequest(new { message = "Eres administrador: puedes ejecutar la acción directamente." });

            // El rol se lee de la BD, no del token: si a alguien le cambiaron el
            // rol hace un minuto, su token viejo no debe seguir habilitandolo.
            var solicitante = await _context.Usuarios.FindAsync(solicitanteId);
            if (solicitante == null)
                return Unauthorized(new { message = "Usuario no encontrado." });

            if (!LimitesAutorizacion.PuedeSolicitar(solicitante.rol, tipo))
                return StatusCode(403, new
                {
                    message = $"Tu rol ({solicitante.rol}) no puede solicitar esta acción."
                });

            // Evitar que se acumulen peticiones repetidas sobre lo mismo.
            var yaExiste = await _context.SolicitudesAprobacion.AnyAsync(s =>
                s.tipo == tipo &&
                s.entidad_id == dto.entidad_id &&
                s.estado == EstadoSolicitud.PENDIENTE);

            if (yaExiste)
                return Conflict(new { message = "Ya hay una solicitud pendiente para esta misma acción." });

            // Cada tipo arma su contexto: monto y cliente para que el admin vea
            // de que tamano es lo que esta autorizando sin salir de la bandeja.
            int? clienteId = null;
            decimal monto = 0m;
            string descripcion;

            switch (tipo)
            {
                case TipoSolicitud.ELIMINAR_PAGO:
                {
                    var pago = await _context.Pagos.FindAsync(dto.entidad_id);
                    if (pago == null)
                        return NotFound(new { message = $"El pago #{dto.entidad_id} no existe." });

                    monto = pago.monto_pagado;
                    clienteId = await _context.Prestamos
                        .Where(p => p.prestamo_id == pago.prestamo_id)
                        .Select(p => (int?)p.cliente_id)
                        .FirstOrDefaultAsync();

                    descripcion = $"Eliminar el pago #{pago.pago_id} de {monto:C2} " +
                                  $"del crédito #{pago.prestamo_id}";
                    break;
                }

                case TipoSolicitud.CONDONAR_MORA:
                {
                    var prestamo = await _context.Prestamos.FindAsync(dto.entidad_id);
                    if (prestamo == null)
                        return NotFound(new { message = $"El crédito #{dto.entidad_id} no existe." });

                    monto = await _context.PeriodosAmortizacion
                        .Where(p => p.prestamo_id == dto.entidad_id)
                        .SumAsync(p => (decimal?)p.interes_moratorio) ?? 0m;

                    if (monto <= 0)
                        return BadRequest(new { message = "Ese crédito no tiene mora pendiente por condonar." });

                    clienteId = prestamo.cliente_id;
                    descripcion = $"Condonar mora de {monto:C2} en el crédito #{dto.entidad_id}";
                    break;
                }

                case TipoSolicitud.ELIMINAR_CREDITO:
                {
                    var prestamo = await _context.Prestamos.FindAsync(dto.entidad_id);
                    if (prestamo == null)
                        return NotFound(new { message = $"El crédito #{dto.entidad_id} no existe." });

                    var tienePagos = await _context.Pagos.AnyAsync(p => p.prestamo_id == dto.entidad_id);
                    if (tienePagos)
                        return BadRequest(new { message = "Ese crédito ya tiene pagos registrados; no puede darse de baja." });

                    clienteId = prestamo.cliente_id;
                    monto = prestamo.monto_total;
                    descripcion = $"Dar de baja el crédito #{dto.entidad_id} por {monto:C2}";
                    break;
                }

                case TipoSolicitud.ELIMINAR_CLIENTE:
                {
                    var cliente = await _context.Clientes
                        .Include(c => c.Usuario)
                        .FirstOrDefaultAsync(c => c.cliente_id == dto.entidad_id);

                    if (cliente == null)
                        return NotFound(new { message = $"El cliente #{dto.entidad_id} no existe." });

                    var creditosVivos = await _context.Prestamos.CountAsync(p =>
                        p.cliente_id == dto.entidad_id && p.estatus != EstatusPrestamo.LIQUIDADO);

                    if (creditosVivos > 0)
                        return BadRequest(new { message = $"Ese cliente tiene {creditosVivos} crédito(s) sin liquidar." });

                    clienteId = dto.entidad_id;
                    var nombreCli = cliente.Usuario == null
                        ? $"#{dto.entidad_id}"
                        : $"{cliente.Usuario.nombre} {cliente.Usuario.apellido}".Trim();
                    descripcion = $"Dar de baja al cliente {nombreCli} (#{dto.entidad_id})";
                    break;
                }

                case TipoSolicitud.DAR_BAJA_TRABAJADOR:
                {
                    var trabajador = await _context.Usuarios.FindAsync(dto.entidad_id);
                    if (trabajador == null)
                        return NotFound(new { message = $"El usuario #{dto.entidad_id} no existe." });

                    // Un ADMIN no se da de baja pidiendolo desde abajo.
                    if (trabajador.rol == RolUsuario.ADMIN)
                        return BadRequest(new { message = "No puedes solicitar la baja de un administrador." });

                    if (trabajador.rol == RolUsuario.CLIENTE)
                        return BadRequest(new { message = "Ese usuario es un cliente, no un trabajador. Usa la baja de cliente." });

                    if (trabajador.usuario_id == solicitanteId)
                        return BadRequest(new { message = "No puedes solicitar tu propia baja." });

                    if (trabajador.estado == EstadoUsuario.INACTIVO)
                        return BadRequest(new { message = "Ese trabajador ya está inactivo." });

                    descripcion = $"Dar de baja al trabajador {trabajador.nombre} {trabajador.apellido} " +
                                  $"(#{dto.entidad_id}, {trabajador.rol})";
                    break;
                }

                default:
                    return BadRequest(new { message = $"El tipo {tipo} todavía no está habilitado." });
            }

            var solicitud = await _service.CrearAsync(
                tipo, solicitanteId, dto.entidad_id, dto.justificacion, clienteId, monto, descripcion);

            return CreatedAtAction(nameof(Detalle), new { id = solicitud.id }, new
            {
                message = "Solicitud enviada. Un administrador la revisará.",
                solicitud_id = solicitud.id,
                estado = solicitud.estado.ToString(),
                descripcion,
            });
        }

        // =====================================================
        // GET: api/SolicitudAprobacion?estado=PENDIENTE
        // ADMIN ve todas; el resto solo las suyas.
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] string? estado = null)
        {
            var esAdmin = User.IsInRole(nameof(RolUsuario.ADMIN));
            var usuarioId = UsuarioActual();

            var query = _context.SolicitudesAprobacion
                .Include(s => s.Solicitante)
                .Include(s => s.Resolutor)
                .AsQueryable();

            if (!esAdmin)
                query = query.Where(s => s.solicitante_id == usuarioId);

            if (!string.IsNullOrWhiteSpace(estado) &&
                Enum.TryParse<EstadoSolicitud>(estado, ignoreCase: true, out var estadoFiltro))
                query = query.Where(s => s.estado == estadoFiltro);

            var items = await query
                .OrderByDescending(s => s.created_at)
                .Take(200)
                .Select(s => new
                {
                    s.id,
                    tipo   = s.tipo.ToString(),
                    estado = s.estado.ToString(),
                    s.entidad_id,
                    s.cliente_id,
                    s.monto,
                    s.justificacion,
                    s.descripcion,
                    s.respuesta,
                    s.created_at,
                    s.resuelta_at,
                    solicitante = s.Solicitante == null
                        ? null
                        : (s.Solicitante.nombre + " " + s.Solicitante.apellido).Trim(),
                    solicitante_rol = s.Solicitante == null ? null : s.Solicitante.rol.ToString(),
                    resuelta_por = s.Resolutor == null
                        ? null
                        : (s.Resolutor.nombre + " " + s.Resolutor.apellido).Trim(),
                })
                .ToListAsync();

            return Ok(items);
        }

        // =====================================================
        // GET: api/SolicitudAprobacion/pendientes/count
        // Alimenta el badge de la campana del admin.
        // =====================================================
        [HttpGet("pendientes/count")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> ContarPendientes()
        {
            var total = await _context.SolicitudesAprobacion
                .CountAsync(s => s.estado == EstadoSolicitud.PENDIENTE);

            return Ok(new { pendientes = total });
        }

        // =====================================================
        // GET: api/SolicitudAprobacion/5
        // =====================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detalle(int id)
        {
            var s = await _context.SolicitudesAprobacion
                .Include(x => x.Solicitante)
                .Include(x => x.Resolutor)
                .FirstOrDefaultAsync(x => x.id == id);

            if (s == null) return NotFound(new { message = "Solicitud no encontrada." });

            // El solicitante puede ver la suya; el resto de detalles, solo ADMIN.
            if (!User.IsInRole(nameof(RolUsuario.ADMIN)) && s.solicitante_id != UsuarioActual())
                return Forbid();

            return Ok(new
            {
                s.id,
                tipo   = s.tipo.ToString(),
                estado = s.estado.ToString(),
                s.entidad_id,
                s.cliente_id,
                s.monto,
                s.justificacion,
                s.descripcion,
                s.respuesta,
                s.created_at,
                s.resuelta_at,
                solicitante = s.Solicitante == null
                    ? null
                    : (s.Solicitante.nombre + " " + s.Solicitante.apellido).Trim(),
                solicitante_rol = s.Solicitante == null ? null : s.Solicitante.rol.ToString(),
                resuelta_por = s.Resolutor == null
                    ? null
                    : (s.Resolutor.nombre + " " + s.Resolutor.apellido).Trim(),
            });
        }

        // =====================================================
        // POST: api/SolicitudAprobacion/5/aprobar
        // =====================================================
        [HttpPost("{id:int}/aprobar")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Aprobar(int id, [FromBody] ResolverSolicitudDto? dto)
        {
            var r = await _service.AprobarAsync(id, UsuarioActual(), dto?.respuesta);

            if (!r.Ok)
                return BadRequest(new { message = r.Mensaje, estado = r.EstadoFinal.ToString() });

            return Ok(new { message = r.Mensaje, estado = r.EstadoFinal.ToString() });
        }

        // =====================================================
        // POST: api/SolicitudAprobacion/5/rechazar
        // El motivo es obligatorio: el solicitante debe entender por que.
        // =====================================================
        [HttpPost("{id:int}/rechazar")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Rechazar(int id, [FromBody] ResolverSolicitudDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.respuesta))
                return BadRequest(new { message = "Explica el motivo del rechazo: el solicitante necesita entender la decisión." });

            var r = await _service.RechazarAsync(id, UsuarioActual(), dto.respuesta);

            if (!r.Ok)
                return BadRequest(new { message = r.Mensaje, estado = r.EstadoFinal.ToString() });

            return Ok(new { message = r.Mensaje, estado = r.EstadoFinal.ToString() });
        }

        // =====================================================
        // POST: api/SolicitudAprobacion/5/cancelar
        // El propio solicitante retira su peticion.
        // =====================================================
        [HttpPost("{id:int}/cancelar")]
        public async Task<IActionResult> Cancelar(int id)
        {
            var s = await _context.SolicitudesAprobacion.FindAsync(id);
            if (s == null) return NotFound(new { message = "Solicitud no encontrada." });

            if (s.solicitante_id != UsuarioActual())
                return Forbid();

            if (s.estado != EstadoSolicitud.PENDIENTE)
                return BadRequest(new { message = $"La solicitud ya fue resuelta ({s.estado})." });

            s.estado      = EstadoSolicitud.CANCELADA;
            s.resuelta_at = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Solicitud cancelada.", estado = s.estado.ToString() });
        }
    }
}
