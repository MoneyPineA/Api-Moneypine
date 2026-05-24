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

        public PagoController(AppDbContext context, ActivityService activityService, NotificationService notificationService)
        {
            _context = context;
            _activityService = activityService;
            _notificationService = notificationService;
        }

        // =====================================================
        // GET: api/Pago/cobros-realizados
        // Reporte enriquecido: pagos + datos del préstamo y cliente
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
                .ToListAsync();

            var cobradorIds = pagos
                .Where(p => p.cobrador_id.HasValue)
                .Select(p => p.cobrador_id!.Value)
                .Distinct().ToList();

            var cobradores = await _context.Usuarios
                .Where(u => cobradorIds.Contains(u.usuario_id))
                .ToDictionaryAsync(u => u.usuario_id, u => $"{u.nombre} {u.apellido}".Trim());

            var result = pagos.Select(p => new
            {
                numero_recibo        = p.pago_id,
                credito              = p.prestamo_id,
                num_socio            = p.Prestamo?.Cliente?.clave_cliente,
                socio                = p.Prestamo?.Cliente?.Usuario != null
                    ? $"{p.Prestamo.Cliente.Usuario.nombre} {p.Prestamo.Cliente.Usuario.apellido} {p.Prestamo.Cliente.apellido_materno}".Trim()
                    : $"Cliente #{p.Prestamo?.cliente_id}",
                ruta                 = p.Prestamo?.destino ?? "—",
                fecha_referencia     = p.fecha_pago.ToString("yyyy-MM-dd"),
                referencia           = (string?)null,
                asesor_aplicador     = p.cobrador_id.HasValue && cobradores.ContainsKey(p.cobrador_id.Value)
                    ? cobradores[p.cobrador_id.Value] : null,
                tipo_abono           = "Parcialidad(interés y capital)",
                cantidad_recibo      = p.monto_pagado,
                fecha_real_aplicacion = p.fecha_pago.ToString("yyyy-MM-dd HH:mm:ss"),
                metodo_pago          = p.metodo_pago,
                p.interes_pagado,
                p.mora_pagada,
                p.saldo_restante,
                p.estatus,
            });

            return Ok(result);
        }

        // =====================================================
        // GET: api/Pago
        // Obtiene todos los pagos
        // =====================================================
        [Authorize]
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
        [Authorize]
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
        [Authorize]
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

            DateTime fechaPago = dto.fecha_pago.HasValue
                ? DateTime.SpecifyKind(dto.fecha_pago.Value.Date, DateTimeKind.Unspecified)
                : TimeHelper.GetMexicoTime();

            if (dto.monto_pagado <= 0)
                return BadRequest("El monto debe ser mayor a 0");

            // ================================
            // MONEYPINE-FIX: cargar periodos pendientes reales
            // ================================

            var periodosPendientes = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .ToListAsync();

            var primerPeriodo = periodosPendientes.FirstOrDefault();

            // ================================
            // 1. CALCULAR MORA
            //    Usa fecha_vencimiento del primer periodo pendiente si existe,
            //    fallback a fecha_proximo_pago del préstamo
            // ================================

            decimal moraAcumulada = 0;
            DateTime fechaRefMora = primerPeriodo?.fecha_vencimiento
                ?? prestamo.fecha_proximo_pago
                ?? fechaPago;

            if (fechaPago.Date > fechaRefMora.Date.AddDays(prestamo.dias_gracia))
            {
                int diasAtraso = (fechaPago.Date - fechaRefMora.Date).Days;
                moraAcumulada = diasAtraso * prestamo.mora_diaria;
            }

            // ================================
            // 2. VALIDAR MONTO
            // ================================

            decimal maxPermitido = prestamo.saldo_actual + moraAcumulada;
            if (dto.monto_pagado > maxPermitido)
                return BadRequest($"El monto ({dto.monto_pagado:N2}) excede el adeudo total ({maxPermitido:N2}). Saldo: ${prestamo.saldo_actual:N2}, Mora: ${moraAcumulada:N2}");

            // MONEYPINE-FIX: acumular pagos del mismo día para evaluar si alcanzan el periodo
            var diaInicio = fechaPago.Date;
            var diaFin    = diaInicio.AddDays(1);
            decimal pagoAcumulado = await _context.Pagos
                .Where(p => p.prestamo_id == dto.prestamo_id
                         && p.fecha_pago >= diaInicio
                         && p.fecha_pago < diaFin
                         && p.estatus == EstatusPago.APLICADO)
                .SumAsync(p => (decimal?)p.monto_pagado) ?? 0m;

            decimal pagoRestante = dto.monto_pagado + pagoAcumulado;

            // ================================
            // 3. ITERAR PERIODOS: marcar como pagado si el monto alcanza
            //    costo_total = abono_capital + interés + mora propia del periodo
            // ================================

            var aMarcarPagados = new List<PeriodoAmortizacion>();
            decimal capitalPagado = 0m;
            decimal interesPagado = 0m;
            decimal moraPagada    = 0m;

            foreach (var p in periodosPendientes)
            {
                int     diasMora     = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                decimal moraPeriodo  = diasMora > 0 ? Math.Round(prestamo.mora_diaria * diasMora, 2) : 0m;
                decimal costoPeriodo = p.abono_capital + p.interes_normal + p.interes_iva + moraPeriodo;

                if (pagoRestante >= costoPeriodo - 0.01m)
                {
                    aMarcarPagados.Add(p);
                    pagoRestante  -= costoPeriodo;
                    capitalPagado += p.abono_capital;
                    interesPagado += p.interes_normal + p.interes_iva;
                    moraPagada    += moraPeriodo;

                    p.estado_pago       = 3;
                    p.fecha_pagado      = fechaPago;
                    p.dias_moratorio    = diasMora;
                    p.interes_moratorio = moraPeriodo;
                }
                else break;
            }

            // ================================
            // 4. ACTUALIZAR SALDO: solo capital abonado, no interés
            // ================================

            // MONEYPINE-FIX: saldo_actual = suma de abono_capital de periodos aún no pagados
            // capital_pendiente es el saldo acumulado (balance), no el aporte por periodo — NO sumar
            prestamo.saldo_actual = periodosPendientes
                .Skip(aMarcarPagados.Count)
                .Sum(p => p.abono_capital);

            // ================================
            // 6. MONEYPINE-FIX: fecha_proximo_pago = siguiente periodo pendiente real
            //    Fallback: avance por calendario si no hay tabla de amortización
            // ================================

            var siguientePendiente = periodosPendientes.Skip(aMarcarPagados.Count).FirstOrDefault();

            if (siguientePendiente != null)
            {
                prestamo.fecha_proximo_pago = siguientePendiente.fecha_vencimiento;
                if (prestamo.estatus == EstatusPrestamo.ATRASADO)
                    prestamo.estatus = EstatusPrestamo.ACTIVO;
            }
            else if (!periodosPendientes.Any() && prestamo.fecha_proximo_pago.HasValue)
            {
                // Fallback calendario (préstamos sin tabla de amortización)
                decimal montoPeriodo = capitalPagado + interesPagado;
                if (montoPeriodo >= prestamo.pago_mes - 0.01m)
                {
                    int nPeriodos = Math.Max(1, (int)Math.Floor(montoPeriodo / prestamo.pago_mes));
                    prestamo.fecha_proximo_pago = prestamo.forma_pago switch
                    {
                        FormasPago.DIARIA     => prestamo.fecha_proximo_pago.Value.AddDays(nPeriodos),
                        FormasPago.SEMANAL    => prestamo.fecha_proximo_pago.Value.AddDays(7 * nPeriodos),
                        FormasPago.CATORCENAL => prestamo.fecha_proximo_pago.Value.AddDays(14 * nPeriodos),
                        FormasPago.QUINCENAL  => prestamo.fecha_proximo_pago.Value.AddDays(15 * nPeriodos),
                        _                    => prestamo.fecha_proximo_pago.Value.AddMonths(nPeriodos),
                    };
                    if (prestamo.estatus == EstatusPrestamo.ATRASADO)
                        prestamo.estatus = EstatusPrestamo.ACTIVO;
                }
                else if (moraAcumulada > 0)
                {
                    prestamo.estatus = EstatusPrestamo.ATRASADO;
                }
            }

            if (moraAcumulada > 0 && !aMarcarPagados.Any() && periodosPendientes.Any())
                prestamo.estatus = EstatusPrestamo.ATRASADO;

            // ================================
            // 7. VERIFICAR LIQUIDACIÓN
            // ================================

            if (prestamo.saldo_actual <= 0)
            {
                prestamo.estatus  = EstatusPrestamo.LIQUIDADO;
                prestamo.fecha_fin = fechaPago;
            }

            // ================================
            // 8. CREAR REGISTRO DE PAGO
            // ================================

            int? usuarioAplicadorId = null;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var parsedUserId))
                usuarioAplicadorId = parsedUserId;

            var pago = new Pago
            {
                prestamo_id    = prestamo.prestamo_id,
                cobrador_id    = usuarioAplicadorId ?? dto.cobrador_id,
                fecha_pago     = fechaPago,
                monto_pagado   = dto.monto_pagado,
                interes_pagado = interesPagado,
                mora_pagada    = moraPagada,
                saldo_restante = prestamo.saldo_actual,
                metodo_pago    = dto.metodo_pago,
                estatus        = EstatusPago.APLICADO
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

            return CreatedAtAction(nameof(GetPago), new { id = pago.pago_id }, pago);
        }



        // =====================================================
        // PUT: api/Pago/5
        // NO se permite modificar pagos aplicados
        // =====================================================
        [Authorize(Roles = "ADMIN")]
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
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var pago = await _context.Pagos.FindAsync(id);
            if (pago == null)
                return NotFound("Pago no encontrado");

            // DEBUG-LOG-1: pago encontrado
            Console.WriteLine($"[DELETE-PAGO] pago_id={pago.pago_id} prestamo_id={pago.prestamo_id} monto_pagado={pago.monto_pagado} estatus={pago.estatus}");

            var prestamo = await _context.Prestamos.FindAsync(pago.prestamo_id);
            if (prestamo == null)
                return NotFound("Préstamo asociado no encontrado");

            // MONEYPINE-FIX: saldo_actual derivado de periodos pendientes, no aritmética manual
            // Los periodos revertidos aún no están en DB (SaveChanges no ejecutado), los sumamos por separado

            // MONEYPINE-FIX: buscar TODOS los periodos marcados por este pago
            // Criterios: mismo prestamo_id + estado_pago IN (2,3) + fecha_pagado coincide con fecha_pago del recibo
            // estado_pago=2 cubre datos importados del sistema viejo; estado_pago=3 cubre pagos registrados por la API
            var periodosARevertir = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == pago.prestamo_id
                          && (pa.estado_pago == 2 || pa.estado_pago == 3)
                          && pa.fecha_pagado.HasValue
                          && pa.fecha_pagado.Value.Date == pago.fecha_pago.Date)
                .OrderBy(pa => pa.periodo)
                .ToListAsync();

            Console.WriteLine($"[DELETE-PAGO] periodos a revertir: {periodosARevertir.Count} (fecha_pago={pago.fecha_pago:yyyy-MM-dd})");

            // MONEYPINE-FIX: revertir TODOS los periodos encontrados, no solo el primero
            DateTime? fechaVencimientoMasAntigua = null;
            foreach (var p in periodosARevertir)
            {
                Console.WriteLine($"[DELETE-PAGO] revirtiendo periodo_id={p.periodo_id} periodo={p.periodo} estado_pago={p.estado_pago} fecha_vencimiento={p.fecha_vencimiento:yyyy-MM-dd}");
                p.estado_pago       = 1;
                p.fecha_pagado      = null;
                p.dias_moratorio    = 0;
                p.interes_moratorio = 0;
                _context.PeriodosAmortizacion.Update(p);

                // Guardar el vencimiento más antiguo para restaurar fecha_proximo_pago
                if (fechaVencimientoMasAntigua == null || p.fecha_vencimiento < fechaVencimientoMasAntigua)
                    fechaVencimientoMasAntigua = p.fecha_vencimiento;
            }

            // MONEYPINE-FIX: restaurar fecha_proximo_pago al vencimiento más antiguo revertido
            if (fechaVencimientoMasAntigua.HasValue)
                prestamo.fecha_proximo_pago = fechaVencimientoMasAntigua;

            // MONEYPINE-FIX: saldo_actual = suma de abono_capital de periodos pendientes en DB + revertidos
            // abono_capital = capital de ese periodo; capital_pendiente es el balance acumulado (NO sumar)
            decimal saldoYaPendiente = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == pago.prestamo_id && pa.estado_pago == 1)
                .SumAsync(pa => pa.abono_capital);
            prestamo.saldo_actual = saldoYaPendiente + periodosARevertir.Sum(p => p.abono_capital);

            // MONEYPINE-FIX: si estaba LIQUIDADO, revertir a ATRASADO al eliminar un pago
            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                prestamo.estatus = EstatusPrestamo.ATRASADO;

            _context.Pagos.Remove(pago);
            _context.Prestamos.Update(prestamo);

            Console.WriteLine($"[DELETE-PAGO] ANTES SaveChanges: saldo_actual={prestamo.saldo_actual} estatus={prestamo.estatus} fecha_proximo_pago={prestamo.fecha_proximo_pago:yyyy-MM-dd} periodos_revertidos={periodosARevertir.Count}");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            Console.WriteLine($"[DELETE-PAGO] SaveChanges OK — pago {id} eliminado, {periodosARevertir.Count} periodos revertidos a estado_pago=1");

            return NoContent();
        }
    }
}