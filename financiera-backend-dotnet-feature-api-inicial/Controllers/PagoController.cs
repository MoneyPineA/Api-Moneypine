using Microsoft.AspNetCore.Mvc;
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

        public PagoController(AppDbContext context, ActivityService activityService, NotificationService notificationService)
        {
            _context = context;
            _activityService = activityService;
            _notificationService = notificationService;
        }

        // =====================================================
        // GET: api/Pago
        // Obtiene todos los pagos
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetPagos([FromQuery] int? prestamo_id = null)
        {
            var query = _context.Pagos
                .Include(p => p.Prestamo)
                .AsQueryable();

            if (prestamo_id.HasValue)
                query = query.Where(p => p.prestamo_id == prestamo_id.Value);

            var pagos = await query
                .OrderByDescending(p => p.fecha_pago)
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
                    ? cobradores[p.cobrador_id.Value]
                    : null,
                p.fecha_pago,
                p.monto_pagado,
                p.interes_pagado,
                p.mora_pagada,
                p.saldo_restante,
                p.metodo_pago,
                p.estatus
            });

            return Ok(result);
        }

        // =====================================================
        // GET: api/Pago/5
        // Obtiene un pago por su ID
        // =====================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPago(int id)
        {
            var pago = await _context.Pagos
                .Include(p => p.Prestamo)
                .FirstOrDefaultAsync(p => p.pago_id == id);

            if (pago == null)
                return NotFound("Pago no encontrado");

            string? empleadoAplicador = null;
            if (pago.cobrador_id.HasValue)
            {
                empleadoAplicador = await _context.Usuarios
                    .Where(u => u.usuario_id == pago.cobrador_id.Value)
                    .Select(u => $"{u.nombre} {u.apellido}".Trim())
                    .FirstOrDefaultAsync();
            }

            return Ok(new
            {
                pago.pago_id,
                pago.prestamo_id,
                pago.cobrador_id,
                empleado_aplicador = empleadoAplicador,
                pago.fecha_pago,
                pago.monto_pagado,
                pago.interes_pagado,
                pago.mora_pagada,
                pago.saldo_restante,
                pago.metodo_pago,
                pago.estatus
            });
        }

        // =====================================================
        // POST: api/Pago
        // Registra un pago y actualiza el préstamo
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PagoCreateDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var prestamo = await _context.Prestamos
                .FirstOrDefaultAsync(p => p.prestamo_id == dto.prestamo_id);

            if (prestamo == null)
                return BadRequest("El préstamo no existe");

            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                return BadRequest("El préstamo ya está liquidado");

            DateTime fechaPago;

            if (dto.fecha_pago.HasValue)
            {
                fechaPago = TimeHelper.ConvertToMexicoTime(dto.fecha_pago.Value);
            }
            else
            {
                fechaPago = TimeHelper.GetMexicoTime();
            }

            if (dto.monto_pagado <= 0)
                return BadRequest("El monto debe ser mayor a 0");

            if (!prestamo.fecha_proximo_pago.HasValue)
                return BadRequest("El préstamo no tiene fecha de próximo pago definida.");

            // ================================
            // 1. CALCULAR MORA (antes de validar monto)
            // ================================

            decimal moraAcumulada = 0;

            if (fechaPago > prestamo.fecha_proximo_pago.Value.AddDays(prestamo.dias_gracia))
            {
                int diasAtraso = (fechaPago - prestamo.fecha_proximo_pago.Value).Days;
                moraAcumulada = diasAtraso * prestamo.mora_diaria;
            }

            // ================================
            // 2. VALIDAR MONTO (saldo + mora)
            //    saldo_actual = capital + interés pendiente
            //    moraAcumulada = cargo adicional por atraso
            // ================================

            decimal maxPermitido = prestamo.saldo_actual + moraAcumulada;
            if (dto.monto_pagado > maxPermitido)
                return BadRequest($"El monto ({dto.monto_pagado:N2}) excede el adeudo total ({maxPermitido:N2}). Saldo: ${prestamo.saldo_actual:N2}, Mora: ${moraAcumulada:N2}");

            decimal montoDisponible = dto.monto_pagado;

            // ================================
            // 3. APLICAR PAGO: mora → interés → capital
            // ================================

            decimal moraPagada = Math.Min(montoDisponible, moraAcumulada);
            montoDisponible -= moraPagada;

            decimal interesTotal = prestamo.monto_total - prestamo.monto;
            decimal interesPorPeriodo = prestamo.plazo_meses > 0
                ? interesTotal / prestamo.plazo_meses
                : 0;
            decimal interesPagado = Math.Min(montoDisponible, interesPorPeriodo);
            montoDisponible -= interesPagado;

            decimal capitalPagado = montoDisponible;

            // ================================
            // 4. ACTUALIZAR SALDO
            //    Solo resta capital + interés (estaban en monto_total).
            //    La mora es cargo adicional, NO reduce el saldo original.
            // ================================

            prestamo.saldo_actual -= (capitalPagado + interesPagado);
            prestamo.saldo_actual = Math.Max(prestamo.saldo_actual, 0);

            // ================================
            // 5. AVANZAR FECHA PRÓXIMO PAGO según forma_pago
            //    Solo avanza si el pago (sin mora) cubre al menos 1 periodo
            // ================================

            decimal montoPeriodo = capitalPagado + interesPagado;
            if (montoPeriodo >= prestamo.pago_mes - 0.01m)
            {
                int periodosCompletos = Math.Max(1, (int)Math.Floor(montoPeriodo / prestamo.pago_mes));

                switch (prestamo.forma_pago)
                {
                    case FormasPago.DIARIA:
                        prestamo.fecha_proximo_pago = prestamo.fecha_proximo_pago.Value.AddDays(1 * periodosCompletos);
                        break;
                    case FormasPago.SEMANAL:
                        prestamo.fecha_proximo_pago = prestamo.fecha_proximo_pago.Value.AddDays(7 * periodosCompletos);
                        break;
                    case FormasPago.CATORCENAL:
                        prestamo.fecha_proximo_pago = prestamo.fecha_proximo_pago.Value.AddDays(14 * periodosCompletos);
                        break;
                    case FormasPago.QUINCENAL:
                        prestamo.fecha_proximo_pago = prestamo.fecha_proximo_pago.Value.AddDays(15 * periodosCompletos);
                        break;
                    case FormasPago.MENSUAL:
                    default:
                        prestamo.fecha_proximo_pago = prestamo.fecha_proximo_pago.Value.AddMonths(periodosCompletos);
                        break;
                }

                if (prestamo.estatus == EstatusPrestamo.ATRASADO)
                    prestamo.estatus = EstatusPrestamo.ACTIVO;
            }
            else if (moraAcumulada > 0)
            {
                prestamo.estatus = EstatusPrestamo.ATRASADO;
            }

            // ================================
            // 6. VERIFICAR LIQUIDACIÓN
            // ================================

            if (prestamo.saldo_actual <= 0)
            {
                prestamo.estatus = EstatusPrestamo.LIQUIDADO;
                prestamo.fecha_fin = fechaPago;
            }

            // ================================
            // 7. CREAR REGISTRO DE PAGO
            //    monto_pagado = total pagado por el cliente (NO solo capital)
            //    interes_pagado y mora_pagada = desglose
            //    capital = monto_pagado - interes_pagado - mora_pagada
            // ================================

            int? usuarioAplicadorId = null;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var parsedUserId))
                usuarioAplicadorId = parsedUserId;

            var pago = new Pago
            {
                prestamo_id = prestamo.prestamo_id,
                cobrador_id = usuarioAplicadorId ?? dto.cobrador_id,
                fecha_pago = fechaPago,
                monto_pagado = dto.monto_pagado,
                interes_pagado = interesPagado,
                mora_pagada = moraPagada,
                saldo_restante = prestamo.saldo_actual,
                metodo_pago = dto.metodo_pago,
                estatus = EstatusPago.APLICADO
            };

            _context.Pagos.Add(pago);
            _context.Prestamos.Update(prestamo);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            await _activityService.CreateActivity(
            ActivityType.PAYMENT_RECEIVED,
            prestamo.cliente_id,
            capitalPagado + interesPagado + moraPagada,
            NotificationLevel.POSITIVE
        );

            var msg = prestamo.estatus == EstatusPrestamo.LIQUIDADO
                ? $"Préstamo #{prestamo.prestamo_id} liquidado completamente"
                : $"Pago registrado: ${dto.monto_pagado:N2} al préstamo #{prestamo.prestamo_id}";
            var lvl = prestamo.estatus == EstatusPrestamo.LIQUIDADO
                ? NotificationLevel.POSITIVE : NotificationLevel.NEUTRAL;
            await _notificationService.CreateNotification(1, msg, lvl);

            return CreatedAtAction(nameof(GetPago),
                new { id = pago.pago_id }, pago);
        }



        // =====================================================
        // PUT: api/Pago/5
        // NO se permite modificar pagos aplicados
        // =====================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Pago pago)
        {
            if (id != pago.pago_id)
                return BadRequest("El ID no coincide");

            var existente = await _context.Pagos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.pago_id == id);

            if (existente == null)
                return NotFound("Pago no encontrado");

            // Regla de negocio: pagos aplicados NO se modifican
            if (existente.estatus == EstatusPago.APLICADO)
                return BadRequest("No se puede modificar un pago aplicado");

            _context.Entry(pago).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =====================================================
        // DELETE: api/Pago/5
        // NO se permite eliminar pagos aplicados
        // =====================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var pago = await _context.Pagos.FindAsync(id);

            if (pago == null)
                return NotFound("Pago no encontrado");

            if (pago.estatus == EstatusPago.APLICADO)
                return BadRequest("No se puede eliminar un pago aplicado");

            _context.Pagos.Remove(pago);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}