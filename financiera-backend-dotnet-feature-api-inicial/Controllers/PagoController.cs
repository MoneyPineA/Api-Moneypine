using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using ApiEjemplo.Services;
using System.Security.Claims;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PagoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityService _activityService;
        private readonly NotificationService _notificationService;
        private readonly AplicacionPagoService _motorPago;
        private readonly MotorRecalculoPrestamoService _motorRecalculo;

        public PagoController(AppDbContext context, ActivityService activityService,
            NotificationService notificationService, AplicacionPagoService motorPago,
            MotorRecalculoPrestamoService motorRecalculo)
        {
            _context = context;
            _activityService = activityService;
            _notificationService = notificationService;
            _motorPago = motorPago;
            _motorRecalculo = motorRecalculo;
        }

        // =====================================================
        // GET: api/Pago/cobros-realizados
        // =====================================================
        [Authorize]
        [HttpGet("cobros-realizados")]
        public async Task<IActionResult> GetCobrosRealizados(
            [FromQuery] DateTime? desde      = null,
            [FromQuery] DateTime? hasta      = null,
            [FromQuery] string?  metodo_pago = null,
            [FromQuery] int?     cobrador_id = null)
        {
            var query = _context.Pagos
                .Include(p => p.Prestamo)
                    .ThenInclude(pr => pr.Cliente)
                        .ThenInclude(c => c.Usuario)
                .AsQueryable();

            if (desde.HasValue)
                query = query.Where(p => p.fecha_pago >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(p => p.fecha_pago <= hasta.Value.AddDays(1));
            if (!string.IsNullOrEmpty(metodo_pago) && metodo_pago != "Todos")
                query = query.Where(p => p.metodo_pago == metodo_pago);
            if (cobrador_id.HasValue)
                query = query.Where(p => p.cobrador_id == cobrador_id.Value);

            var pagos = await query
                .OrderByDescending(p => p.fecha_pago)
                .ThenByDescending(p => p.pago_id)
                .ToListAsync();

            var cobradorIds = pagos.Where(p => p.cobrador_id.HasValue)
                .Select(p => p.cobrador_id!.Value).Distinct().ToList();
            var cobradores = await _context.Usuarios
                .Where(u => cobradorIds.Contains(u.usuario_id))
                .ToDictionaryAsync(u => u.usuario_id, u => $"{u.nombre} {u.apellido}".Trim());

            var result = pagos.Select(p => new
            {
                numero_recibo        = p.pago_id,
                credito              = p.prestamo_id,
                num_socio            = p.Prestamo?.Cliente?.clave_cliente,
                socio                = p.Prestamo?.Cliente?.Usuario != null
                    ? $"{p.Prestamo.Cliente.Usuario.nombre} {p.Prestamo.Cliente.apellido_paterno} {p.Prestamo.Cliente.apellido_materno}".Trim()
                    : $"Cliente #{p.Prestamo?.cliente_id}",
                ruta                 = p.Prestamo?.destino ?? "—",
                fecha_referencia     = p.fecha_pago.ToString("yyyy-MM-dd"),
                referencia           = (string?)null,
                asesor_aplicador     = p.cobrador_id.HasValue && cobradores.ContainsKey(p.cobrador_id.Value)
                    ? cobradores[p.cobrador_id.Value] : null,
                tipo_abono           = p.tipo_pago ?? "parcialidad_mora",
                cantidad_recibo      = p.monto_pagado,
                fecha_real_aplicacion = p.fecha_pago.ToString("yyyy-MM-dd HH:mm:ss"),
                metodo_pago          = p.metodo_pago,
                p.interes_pagado,
                p.interes_iva,
                p.mora_pagada,
                p.abono_capital,
                p.saldo_restante,
                p.estatus,
            });

            return Ok(result);
        }

        // =====================================================
        // GET: api/Pago
        // =====================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetPagos([FromQuery] int? prestamo_id = null)
        {
            var query = _context.Pagos.Include(p => p.Prestamo).AsQueryable();
            if (prestamo_id.HasValue)
                query = query.Where(p => p.prestamo_id == prestamo_id.Value);

            var pagos = await query
                .OrderByDescending(p => p.fecha_pago)
                .ThenByDescending(p => p.pago_id)
                .ToListAsync();

            var cobradorIds = pagos
                .Where(p => p.cobrador_id.HasValue)
                .Select(p => p.cobrador_id!.Value)
                .Distinct()
                .ToList();
            var cobradores = await _context.Usuarios
                .Where(u => cobradorIds.Contains(u.usuario_id))
                .ToDictionaryAsync(u => u.usuario_id, u => $"{u.nombre} {u.apellido}".Trim());

            var result = pagos.Select(p => new
            {
                p.pago_id,
                p.prestamo_id,
                p.cobrador_id,
                empleado_aplicador = p.cobrador_id.HasValue && cobradores.ContainsKey(p.cobrador_id.Value)
                    ? cobradores[p.cobrador_id.Value] : null,
                p.fecha_pago,
                p.monto_pagado,
                p.interes_pagado,
                p.interes_iva,
                p.mora_pagada,
                p.abono_capital,
                p.saldo_restante,
                p.metodo_pago,
                p.tipo_pago,
                p.estatus,
            });

            return Ok(result);
        }

        // =====================================================
        // GET: api/Pago/5
        // =====================================================
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPago(int id)
        {
            var pago = await _context.Pagos.Include(p => p.Prestamo).FirstOrDefaultAsync(p => p.pago_id == id);
            if (pago == null) return NotFound("Pago no encontrado");

            string? emp = null;
            if (pago.cobrador_id.HasValue)
                emp = await _context.Usuarios
                    .Where(u => u.usuario_id == pago.cobrador_id.Value)
                    .Select(u => $"{u.nombre} {u.apellido}".Trim())
                    .FirstOrDefaultAsync();

            return Ok(new
            {
                pago.pago_id,
                pago.prestamo_id,
                pago.cobrador_id,
                empleado_aplicador = emp,
                pago.fecha_pago,
                pago.monto_pagado,
                pago.interes_pagado,
                pago.interes_iva,
                pago.mora_pagada,
                pago.abono_capital,
                pago.saldo_restante,
                pago.metodo_pago,
                pago.tipo_pago,
                pago.estatus,
            });
        }

        // =====================================================
        // POST: api/Pago/preview
        // Calcula la distribución sin aplicar ni guardar 
        // =====================================================
        [Authorize]
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] PagoCreateDTO dto)
        {
            if (!AplicacionPagoService.TipoPagoValido(dto.tipo_pago))
                return BadRequest($"tipo_pago '{dto.tipo_pago}' no válido.");

            var res = await _motorPago.CalcularDistribucion(dto);
            if (!res.ok)
                return BadRequest(res.error);

            return Ok(new
            {
                res.capital_total,
                res.interes_total,
                res.iva_total,
                res.mora_total,
                res.saldo_nuevo,
                res.tipo_pago,
                detalles = res.detalles.Select(d => new
                {
                    d.periodo_id,
                    d.periodo_num,
                    d.capital_aplicado,
                    d.interes_aplicado,
                    d.iva_aplicado,
                    d.mora_aplicada,
                    d.periodo_cerrado,
                }),
            });
        }

        // =====================================================
        // POST: api/Pago
        // =====================================================
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PagoCreateDTO dto)
        {
            // 1. Normalizar tipo_pago (null → parcialidad)
            string tipoPago = dto.tipo_pago ?? "parcialidad";
            dto.tipo_pago = tipoPago;

            if (!AplicacionPagoService.TipoPagoValido(tipoPago))
                return BadRequest($"tipo_pago '{dto.tipo_pago}' no válido.");

            if (dto.monto_pagado <= 0)
                return BadRequest("El monto debe ser mayor a 0.");

            // 2. Validar monto máximo (sin efectos secundarios)
            var dist = await _motorPago.CalcularDistribucion(dto);
            if (!dist.ok) return BadRequest(dist.error);

            // 3. Cargar préstamo
            var prestamo = await _context.Prestamos.FindAsync(dto.prestamo_id);
            if (prestamo == null) return BadRequest("El préstamo no existe");

            DateTime fechaPago = dto.fecha_pago.HasValue
                ? DateTime.SpecifyKind(dto.fecha_pago.Value.Date, DateTimeKind.Unspecified)
                : TimeHelper.GetMexicoTime();

            int? usuarioId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var pid))
                usuarioId = pid;

            // 4. Insertar pago — distribución calculada por Reconstruir
            var pago = new Pago
            {
                prestamo_id  = prestamo.prestamo_id,
                cobrador_id  = usuarioId ?? dto.cobrador_id,
                fecha_pago   = fechaPago,
                monto_pagado = dto.monto_pagado,
                metodo_pago  = dto.metodo_pago,
                tipo_pago    = tipoPago,
                estatus      = EstatusPago.APLICADO,
            };
            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            // 5. Reconstruir estado completo del préstamo
            await _motorRecalculo.Reconstruir(dto.prestamo_id);

            // 6. Recargar con distribución actualizada
            pago     = await _context.Pagos.FindAsync(pago.pago_id) ?? pago;
            prestamo = await _context.Prestamos.FindAsync(dto.prestamo_id) ?? prestamo;

            // 7. Actividad y notificación
            var nombreCliente = await _context.Clientes
                .Where(c => c.cliente_id == prestamo.cliente_id)
                .Include(c => c.Usuario)
                .Select(c => c.Usuario != null
                    ? $"{c.Usuario.nombre} {c.Usuario.apellido}"
                    : $"Cliente #{prestamo.cliente_id}")
                .FirstOrDefaultAsync() ?? $"Cliente #{prestamo.cliente_id}";

            await _activityService.CreateActivity(
                ActivityType.PAYMENT_RECEIVED,
                prestamo.cliente_id,
                dto.monto_pagado,
                NotificationLevel.POSITIVE,
                description: $"Pago de ${dto.monto_pagado:N2} ({tipoPago}) al crédito #{prestamo.prestamo_id} de {nombreCliente}",
                userId: usuarioId);

            var msg = prestamo.estatus == EstatusPrestamo.LIQUIDADO
                ? $"Préstamo #{prestamo.prestamo_id} liquidado completamente"
                : $"Pago registrado: ${dto.monto_pagado:N2} al préstamo #{prestamo.prestamo_id}";
            await _notificationService.CreateNotification(1, msg,
                prestamo.estatus == EstatusPrestamo.LIQUIDADO
                    ? NotificationLevel.POSITIVE : NotificationLevel.NEUTRAL);

            return CreatedAtAction(nameof(GetPago), new { id = pago.pago_id }, new
            {
                pago.pago_id,
                pago.prestamo_id,
                pago.fecha_pago,
                pago.monto_pagado,
                pago.interes_pagado,
                pago.interes_iva,
                pago.mora_pagada,
                pago.abono_capital,
                pago.saldo_restante,
                pago.metodo_pago,
                pago.tipo_pago,
                pago.estatus,
            });
        }

        // =====================================================
        // PUT: api/Pago/5 — solo ADMIN, pagos no APLICADO
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Pago pago)
        {
            if (id != pago.pago_id) return BadRequest("El ID no coincide");
            var existente = await _context.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.pago_id == id);
            if (existente == null) return NotFound("Pago no encontrado");
            if (existente.estatus == EstatusPago.APLICADO)
                return BadRequest("No se puede modificar un pago aplicado");
            _context.Entry(pago).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =====================================================
        // DELETE: api/Pago/5
        // Elimina el pago y reconstruye el estado completo del préstamo.
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var pago = await _context.Pagos.FindAsync(id);
            if (pago == null) return NotFound("Pago no encontrado");

            int prestamoId = pago.prestamo_id;

            _context.Pagos.Remove(pago);
            await _context.SaveChangesAsync();

            await _motorRecalculo.Reconstruir(prestamoId);

            return NoContent();
        }

        // =====================================================
        // POST: api/Pago/recalibrar/{prestamoId}
        // Reconstruye completamente el estado del préstamo.
        // Solo ADMIN.
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpPost("recalibrar/{prestamoId}")]
        public async Task<IActionResult> Recalibrar(int prestamoId)
        {
            var prestamo = await _context.Prestamos.FindAsync(prestamoId);
            if (prestamo == null) return NotFound("Préstamo no encontrado");

            await _motorRecalculo.Reconstruir(prestamoId);

            prestamo = await _context.Prestamos.FindAsync(prestamoId) ?? prestamo;
            var periodos = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == prestamoId).ToListAsync();

            return Ok(new
            {
                message           = $"Préstamo #{prestamoId} recalibrado correctamente.",
                periodosCubiertos = periodos.Count(p => p.estado_pago == 3 || p.estado_pago == 5),
                saldoActual       = prestamo.saldo_actual,
            });
        }
    }
}
