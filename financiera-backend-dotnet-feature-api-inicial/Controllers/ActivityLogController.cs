using Microsoft.AspNetCore.Mvc;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivityLogController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ActivityLogController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // GET: api/ActivityLog
        // Historial completo con filtros y paginación
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] DateTime?      desde   = null,
            [FromQuery] DateTime?      hasta   = null,
            [FromQuery] string?        tipo    = null,
            [FromQuery] int            page    = 1,
            [FromQuery] int            size    = 50)
        {
            var query = _context.ActivityLogs.AsQueryable();

            if (desde.HasValue)
                query = query.Where(a => a.CreatedAt >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(a => a.CreatedAt <= hasta.Value.AddDays(1));
            if (!string.IsNullOrEmpty(tipo) && tipo != "Todos" &&
                Enum.TryParse<ActivityType>(tipo, true, out var t))
                query = query.Where(a => a.Type == t);

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            // Cargar nombres de clientes y usuarios en lote
            var clienteIds = logs.Select(a => a.ClientId).Distinct().ToList();
            var usuarioIds = logs.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();

            var clientes = await _context.Clientes
                .Where(c => clienteIds.Contains(c.cliente_id))
                .Include(c => c.Usuario)
                .ToDictionaryAsync(c => c.cliente_id, c =>
                    c.Usuario != null ? $"{c.Usuario.nombre} {c.Usuario.apellido}".Trim() : $"Cliente #{c.cliente_id}");

            var usuarios = await _context.Usuarios
                .Where(u => usuarioIds.Contains(u.usuario_id))
                .ToDictionaryAsync(u => u.usuario_id, u => $"{u.nombre} {u.apellido}".Trim());

            var result = logs.Select(a => new
            {
                a.Id,
                tipo         = a.Type.ToString(),
                // La etiqueta es SIEMPRE corta (es el badge del historial).
                // El texto largo va en "descripcion" — usar la Description completa
                // aquí rompía el layout con badges de párrafo entero.
                etiqueta     = a.Type switch
                {
                    ActivityType.PAYMENT_RECEIVED => "Pago recibido",
                    ActivityType.CREDIT_APPROVED  => "Crédito aprobado",
                    ActivityType.PAYMENT_OVERDUE  => "Pago vencido",
                    ActivityType.MORA_CONDONADA   => "Mora condonada",
                    ActivityType.PAYMENT_DELETED  => "Pago eliminado",
                    _                             => "Actividad",
                },
                cliente_id   = a.ClientId,
                cliente      = clientes.TryGetValue(a.ClientId, out var cn) ? cn : $"Cliente #{a.ClientId}",
                monto        = a.Amount,
                prioridad    = a.Priority.ToString(),
                color        = a.Priority switch
                {
                    NotificationLevel.HIGH     => "red",
                    NotificationLevel.NEUTRAL  => "yellow",
                    NotificationLevel.POSITIVE => "green",
                    _                          => "gray",
                },
                descripcion  = a.Description,
                usuario_id   = a.UserId,
                usuario      = a.UserId.HasValue && usuarios.TryGetValue(a.UserId.Value, out var un) ? un : null,
                fecha        = a.CreatedAt,
            });

            return Ok(new { total, page, size, items = result });
        }
    }
}
