using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using ApiEjemplo.Helpers;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GestionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GestionController(AppDbContext context)
        {
            _context = context;
        }

        // ===========================================================
        // GET: api/Gestion/prestamos-con-gestion
        // Lista los préstamos que tienen al menos una gestión
        // ===========================================================
        [HttpGet("prestamos-con-gestion")]
        public async Task<IActionResult> GetPrestamosConGestion()
        {
            var prestamoIds = await _context.GestionesCobranza
                .Select(g => g.prestamo_id)
                .Distinct()
                .ToListAsync();

            if (!prestamoIds.Any())
                return Ok(new List<object>());

            var raw = await _context.Prestamos
                .Where(p => prestamoIds.Contains(p.prestamo_id))
                .Include(p => p.Cliente)
                    .ThenInclude(c => c.Usuario)
                .OrderByDescending(p => p.prestamo_id)
                .ToListAsync();

            var gestCounts = await _context.GestionesCobranza
                .Where(g => prestamoIds.Contains(g.prestamo_id))
                .GroupBy(g => g.prestamo_id)
                .Select(g => new { prestamo_id = g.Key, count = g.Count() })
                .ToDictionaryAsync(x => x.prestamo_id, x => x.count);

            // MONEYPINE-FIX: NombreHelper evita duplicar apellido_materno en clientes legacy
            var prestamos = raw.Select(p => new {
                p.prestamo_id,
                p.cliente_id,
                numero_cliente   = p.Cliente?.clave_cliente,
                nombre           = NombreHelper.BuildNombreCliente(
                    p.Cliente?.Usuario?.nombre,
                    p.Cliente?.Usuario?.apellido,
                    p.Cliente?.apellido_materno,
                    p.cliente_id),
                apellido_materno = p.Cliente?.apellido_materno,
                p.monto,
                p.estatus,
                p.fecha_creacion,
                tipo_solicitud   = p.grupo_id.HasValue ? "PRÉSTAMO GRUPAL" : "PRÉSTAMO PERSONAL",
                p.forma_pago,
                total_gestiones  = gestCounts.GetValueOrDefault(p.prestamo_id, 0)
            }).ToList();

            return Ok(prestamos);
        }

        // ===========================================================
        // GET: api/Gestion/prestamo/{prestamoId}
        // Gestiones de un préstamo específico
        // ===========================================================
        [HttpGet("prestamo/{prestamoId}")]
        public async Task<IActionResult> GetByPrestamo(int prestamoId)
        {
            var gestiones = await _context.GestionesCobranza
                .Where(g => g.prestamo_id == prestamoId)
                .Include(g => g.Gestor)
                .OrderByDescending(g => g.fecha_gestion)
                .Select(g => new {
                    g.gestion_id,
                    g.prestamo_id,
                    g.redaccion,
                    g.fecha_gestion,
                    g.fecha_registro,
                    gestor_nombre = g.Gestor != null
                        ? (g.Gestor.nombre ?? "") + " " + (g.Gestor.apellido ?? "")
                        : null,
                })
                .ToListAsync();

            return Ok(gestiones);
        }

        // ===========================================================
        // POST: api/Gestion
        // Crea una nueva gestión de cobranza
        // ===========================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GestionCreateRequest req)
        {
            if (!await _context.Prestamos.AnyAsync(p => p.prestamo_id == req.prestamo_id))
                return NotFound("Préstamo no encontrado");

            var gestion = new GestionCobranza
            {
                prestamo_id    = req.prestamo_id,
                usuario_id     = req.usuario_id,
                redaccion      = req.redaccion,
                fecha_gestion  = req.fecha_gestion,
                fecha_registro = TimeHelper.GetMexicoTime(),
            };

            _context.GestionesCobranza.Add(gestion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByPrestamo),
                new { prestamoId = gestion.prestamo_id },
                new { gestion.gestion_id, gestion.prestamo_id, gestion.redaccion, gestion.fecha_gestion });
        }

        // ===========================================================
        // GET: api/Gestion/notificaciones/{prestamoId}
        // Notificaciones agendadas de un préstamo
        // ===========================================================
        [HttpGet("notificaciones/{prestamoId}")]
        public async Task<IActionResult> GetNotificaciones(int prestamoId)
        {
            var notifs = await _context.NotificacionesAgendadas
                .Where(n => n.prestamo_id == prestamoId)
                .OrderBy(n => n.fecha_hora)
                .Select(n => new {
                    n.notificacion_id,
                    n.titulo,
                    n.detalles,
                    fecha = n.fecha_hora.ToString("yyyy-MM-dd"),
                    hora  = n.fecha_hora.ToString("HH:mm"),
                })
                .ToListAsync();

            return Ok(notifs);
        }

        // ===========================================================
        // POST: api/Gestion/notificaciones
        // Agenda una notificación para un préstamo
        // ===========================================================
        [HttpPost("notificaciones")]
        public async Task<IActionResult> CreateNotificacion([FromBody] NotificacionCreateRequest req)
        {
            if (!await _context.Prestamos.AnyAsync(p => p.prestamo_id == req.prestamo_id))
                return NotFound("Préstamo no encontrado");

            var notif = new NotificacionAgendada
            {
                prestamo_id    = req.prestamo_id,
                titulo         = req.titulo,
                detalles       = req.detalles,
                fecha_hora     = req.fecha_hora,
                fecha_registro = TimeHelper.GetMexicoTime(),
            };

            _context.NotificacionesAgendadas.Add(notif);
            await _context.SaveChangesAsync();

            return Ok(new { notif.notificacion_id, notif.titulo, notif.fecha_hora });
        }
    }

    public class GestionCreateRequest
    {
        public int prestamo_id   { get; set; }
        public int? usuario_id   { get; set; }
        public string redaccion  { get; set; } = string.Empty;
        public DateTime fecha_gestion { get; set; }
    }

    public class NotificacionCreateRequest
    {
        public int prestamo_id    { get; set; }
        public string titulo      { get; set; } = string.Empty;
        public string? detalles   { get; set; }
        public DateTime fecha_hora { get; set; }
    }
}
