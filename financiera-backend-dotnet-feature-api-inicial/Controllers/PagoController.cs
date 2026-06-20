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

        public PagoController(AppDbContext context, ActivityService activityService,
            NotificationService notificationService, AplicacionPagoService motorPago)
        {
            _context = context;
            _activityService = activityService;
            _notificationService = notificationService;
            _motorPago = motorPago;
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

            var pagos = await query.OrderByDescending(p => p.fecha_pago).ToListAsync();

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

            var pagos = await query.OrderByDescending(p => p.fecha_pago).ToListAsync();

            var cobradorIds = pagos.Where(p => p.cobrador_id.HasValue)
                .Select(p => p.cobrador_id!.Value).Distinct().ToList();
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
            // 1. Validar tipo_pago con whitelist
            if (!AplicacionPagoService.TipoPagoValido(dto.tipo_pago))
                return BadRequest($"tipo_pago '{dto.tipo_pago}' no válido.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            // 2. Calcular distribución (puro, sin efectos secundarios)
            var dist = await _motorPago.CalcularDistribucion(dto);
            if (!dist.ok) return BadRequest(dist.error);

            // 3. Cargar préstamo con tracking
            var prestamo = await _context.Prestamos
                .FirstOrDefaultAsync(p => p.prestamo_id == dto.prestamo_id);
            if (prestamo == null) return BadRequest("El préstamo no existe");

            DateTime fechaPago = dto.fecha_pago.HasValue
                ? DateTime.SpecifyKind(dto.fecha_pago.Value.Date, DateTimeKind.Unspecified)
                : TimeHelper.GetMexicoTime();

            int? usuarioId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var pid))
                usuarioId = pid;

            // 4. Crear registro Pago (con saldo_restante estimado; se actualiza abajo)
            var pago = new Pago
            {
                prestamo_id    = prestamo.prestamo_id,
                cobrador_id    = usuarioId ?? dto.cobrador_id,
                fecha_pago     = fechaPago,
                monto_pagado   = dto.monto_pagado,
                interes_pagado = dist.interes_total,
                interes_iva    = dist.iva_total,
                mora_pagada    = dist.mora_total,
                abono_capital  = dist.capital_total,
                saldo_restante = dist.saldo_nuevo,
                metodo_pago    = dto.metodo_pago,
                tipo_pago      = dto.tipo_pago,
                estatus        = EstatusPago.APLICADO,
            };
            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync(); // necesario para obtener pago.pago_id

            // 5. Actualizar ahorro_por_pago y crear registros PagoDetalle
            var periodosAfectados = new HashSet<int>();
            foreach (var det in dist.detalles)
            {
                if (det.periodo_id is null or 0) continue;

                // Mora contribuye a ahorro_por_pago del periodo
                if (det.mora_aplicada > 0)
                {
                    var per = await _context.PeriodosAmortizacion.FindAsync(det.periodo_id.Value);
                    if (per != null)
                    {
                        per.ahorro_por_pago += det.mora_aplicada;
                        _context.PeriodosAmortizacion.Update(per);
                    }
                }

                _context.PagoDetalles.Add(new PagoDetalle
                {
                    pago_id          = pago.pago_id,
                    prestamo_id      = dto.prestamo_id,
                    periodo_id       = det.periodo_id,
                    capital_aplicado = det.capital_aplicado,
                    interes_aplicado = det.interes_aplicado,
                    iva_aplicado     = det.iva_aplicado,
                    mora_aplicada    = det.mora_aplicada,
                    periodo_cerrado  = false, // actualizado en el paso 6
                    tipo_pago        = dto.tipo_pago,
                    fecha_creacion   = TimeHelper.GetMexicoTime(),
                });

                periodosAfectados.Add(det.periodo_id.Value);
            }
            await _context.SaveChangesAsync(); // guarda pago_detalle + ahorro_por_pago

            // 6. Recalcular estado de cada periodo afectado (lee pago_detalle recién guardados)
            var periodoEstados = new Dictionary<int, int>();
            foreach (var periodoId in periodosAfectados)
            {
                var pa = await _context.PeriodosAmortizacion.FindAsync(periodoId);
                if (pa == null) continue;
                int nuevoEstado = await _motorPago.RecalcularEstadoPeriodo(pa, prestamo, fechaPago);
                periodoEstados[periodoId] = nuevoEstado;
                _context.PeriodosAmortizacion.Update(pa);
            }

            // Sincronizar periodo_cerrado en PagoDetalle con el estado real calculado
            var detallesGuardados = await _context.PagoDetalles
                .Where(pd => pd.pago_id == pago.pago_id && pd.periodo_id != null)
                .ToListAsync();
            foreach (var pd in detallesGuardados)
            {
                if (periodoEstados.TryGetValue(pd.periodo_id!.Value, out int est))
                    pd.periodo_cerrado = est == 3 || est == 5;
            }

            // 7. Recalcular saldo, estatus y fecha_proximo_pago del préstamo
            prestamo.saldo_actual = await _motorPago.RecalcularSaldo(dto.prestamo_id, prestamo.monto);
            prestamo.estatus = await _motorPago.RecalcularEstatus(dto.prestamo_id, prestamo.saldo_actual, fechaPago);

            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                prestamo.fecha_fin = fechaPago;

            var siguientePend = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .Select(pa => pa.fecha_vencimiento)
                .FirstOrDefaultAsync();

            if (siguientePend != default)
                prestamo.fecha_proximo_pago = siguientePend;

            // Actualizar saldo_restante en el pago con el valor real recalculado
            pago.saldo_restante = prestamo.saldo_actual;

            _context.Prestamos.Update(prestamo);
            _context.Pagos.Update(pago);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 8. Actividad y notificación
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
                description: $"Pago de ${dto.monto_pagado:N2} ({dto.tipo_pago}) al crédito #{prestamo.prestamo_id} de {nombreCliente}",
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
        // Camino nuevo: revierte exacto via pago_detalle.
        // Camino legado: búsqueda por fecha_pagado para pagos históricos sin detalle.
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var pago = await _context.Pagos.FindAsync(id);
            if (pago == null) return NotFound("Pago no encontrado");

            var prestamo = await _context.Prestamos.FindAsync(pago.prestamo_id);
            if (prestamo == null) return NotFound("Préstamo asociado no encontrado");

            var now = TimeHelper.GetMexicoTime();

            // ── PASO 1: Revertir ahorro_por_pago de mora ──────────────────
            // Aplica tanto a pagos con detalle como legados
            decimal moraARevertir = pago.mora_pagada;
            if (moraARevertir > 0)
            {
                // Primero en congelados (estado_pago=5), luego en pendientes (estado_pago=1)
                var congelados = await _context.PeriodosAmortizacion
                    .Where(pa => pa.prestamo_id == pago.prestamo_id
                              && pa.estado_pago == 5 && pa.ahorro_por_pago > 0)
                    .OrderBy(pa => pa.periodo).ToListAsync();
                foreach (var p in congelados)
                {
                    if (moraARevertir <= 0) break;
                    decimal red = Math.Min(p.ahorro_por_pago, moraARevertir);
                    p.ahorro_por_pago = Math.Max(0m, p.ahorro_por_pago - red);
                    moraARevertir -= red;
                    _context.PeriodosAmortizacion.Update(p);
                }
                if (moraARevertir > 0)
                {
                    var pendMora = await _context.PeriodosAmortizacion
                        .Where(pa => pa.prestamo_id == pago.prestamo_id
                                  && pa.estado_pago == 1 && pa.ahorro_por_pago > 0)
                        .OrderBy(pa => pa.periodo).ToListAsync();
                    foreach (var p in pendMora)
                    {
                        if (moraARevertir <= 0) break;
                        decimal red = Math.Min(p.ahorro_por_pago, moraARevertir);
                        p.ahorro_por_pago = Math.Max(0m, p.ahorro_por_pago - red);
                        moraARevertir -= red;
                        _context.PeriodosAmortizacion.Update(p);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // ── PASO 2: Eliminar el pago (cascade borra pago_detalle) ─────
            _context.Pagos.Remove(pago);
            await _context.SaveChangesAsync();

            // ── PASO 3: CASCADE UNCOVER ───────────────────────────────────
            // Si el pago tenía capital, descubrir períodos desde el más reciente
            // hacia atrás hasta recuperar ese capital (modelo en cascada).
            // Si solo era mora/interés (abono_capital=0), el paso 1 ya lo manejó.
            if (pago.abono_capital > 0.01m)
            {
                decimal capARecuperar = pago.abono_capital;

                // Períodos cubiertos (pagados o congelados), del más reciente al más antiguo
                var cubiertos = await _context.PeriodosAmortizacion
                    .Where(pa => pa.prestamo_id == pago.prestamo_id
                              && (pa.estado_pago == 2 || pa.estado_pago == 3 || pa.estado_pago == 5))
                    .OrderByDescending(pa => pa.periodo)
                    .ToListAsync();

                foreach (var p in cubiertos)
                {
                    if (capARecuperar <= 0.01m) break;

                    // Saltar períodos sin capital asignado: no forman parte
                    // de la cascada de capital y no deben ser descubiertos por ella.
                    if (p.abono_capital <= 0.01m) continue;

                    // Descubrir el período: vuelve a pendiente/atrasado
                    p.estado_pago  = 1;
                    p.fecha_pagado = null;
                    int dias = Math.Max(0, (int)(now.Date - p.fecha_vencimiento.Date).TotalDays);
                    p.dias_moratorio    = dias;
                    p.interes_moratorio = dias > 0 && prestamo.mora_diaria > 0
                        ? Math.Round(prestamo.mora_diaria * dias, 2) : 0m;
                    _context.PeriodosAmortizacion.Update(p);

                    capARecuperar -= p.abono_capital;
                }
                await _context.SaveChangesAsync();
            }

            // ── PASO 4: Recalcular saldo, estatus y fecha_proximo_pago ────
            prestamo.saldo_actual = await _motorPago.RecalcularSaldo(pago.prestamo_id, prestamo.monto);
            prestamo.estatus = await _motorPago.RecalcularEstatus(pago.prestamo_id, prestamo.saldo_actual, now);

            if (prestamo.estatus != EstatusPrestamo.LIQUIDADO)
                prestamo.fecha_fin = null;

            var siguientePend = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == pago.prestamo_id && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .Select(pa => pa.fecha_vencimiento)
                .FirstOrDefaultAsync();

            if (siguientePend != default)
                prestamo.fecha_proximo_pago = siguientePend;

            _context.Prestamos.Update(prestamo);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return NoContent();
        }

        // =====================================================
        // POST: api/Pago/recalibrar/{prestamoId}
        // Recalibra el estado_pago de todos los períodos de un
        // préstamo usando el modelo en cascada: capital pagado
        // acumulado cubre períodos de más antiguo a más nuevo.
        // Solo ADMIN.
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpPost("recalibrar/{prestamoId}")]
        public async Task<IActionResult> Recalibrar(int prestamoId)
        {
            var prestamo = await _context.Prestamos.FindAsync(prestamoId);
            if (prestamo == null) return NotFound("Préstamo no encontrado");

            using var transaction = await _context.Database.BeginTransactionAsync();
            var now = TimeHelper.GetMexicoTime();

            // Capital total pagado (suma desde pago_detalle de pagos APLICADOS)
            var pagosConDetalle = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == prestamoId)
                .Select(pd => pd.pago_id)
                .Distinct()
                .ToListAsync();

            decimal capTotalPagado = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == prestamoId && pd.periodo_id != null)
                .Join(_context.Pagos.Where(p => p.estatus == EstatusPago.APLICADO),
                      pd => pd.pago_id, p => p.pago_id, (pd, _) => pd)
                .SumAsync(pd => (decimal?)pd.capital_aplicado) ?? 0m;

            // Capital de pagos legados sin detalle
            decimal capLegado = await _context.Pagos
                .Where(p => p.prestamo_id == prestamoId
                         && p.estatus == EstatusPago.APLICADO
                         && !pagosConDetalle.Contains(p.pago_id))
                .SumAsync(p => (decimal?)p.abono_capital) ?? 0m;

            decimal capitalDisponible = capTotalPagado + capLegado;

            // Todos los períodos del préstamo ordenados de más antiguo a más nuevo
            var periodos = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == prestamoId)
                .OrderBy(pa => pa.periodo)
                .ToListAsync();

            // Aplicar cascada: cubrir períodos de más antiguo a más nuevo
            // hasta agotar el capital disponible
            foreach (var p in periodos)
            {
                if (p.abono_capital <= 0.01m)
                {
                    // Período sin capital: mantener estado actual sin alterarlo
                    continue;
                }

                if (capitalDisponible >= p.abono_capital - 0.01m)
                {
                    // Período cubierto — marcar como PAGADO o CONGELADO según mora
                    capitalDisponible -= p.abono_capital;
                    if (p.estado_pago != 3 && p.estado_pago != 5)
                    {
                        p.estado_pago  = 3; // PAGADO
                        p.fecha_pagado ??= p.fecha_vencimiento;
                    }
                }
                else
                {
                    // Período no cubierto — reabrir
                    p.estado_pago  = 1;
                    p.fecha_pagado = null;
                    int dias = Math.Max(0, (int)(now.Date - p.fecha_vencimiento.Date).TotalDays);
                    p.dias_moratorio    = dias;
                    p.interes_moratorio = dias > 0 && prestamo.mora_diaria > 0
                        ? Math.Round(prestamo.mora_diaria * dias, 2) : 0m;
                }
                _context.PeriodosAmortizacion.Update(p);
            }
            await _context.SaveChangesAsync();

            // Recalcular saldo, estatus y fecha_proximo_pago del préstamo
            prestamo.saldo_actual = await _motorPago.RecalcularSaldo(prestamoId, prestamo.monto);
            prestamo.estatus = await _motorPago.RecalcularEstatus(prestamoId, prestamo.saldo_actual, now);

            if (prestamo.estatus != EstatusPrestamo.LIQUIDADO)
                prestamo.fecha_fin = null;

            var siguientePend = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == prestamoId && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .Select(pa => pa.fecha_vencimiento)
                .FirstOrDefaultAsync();

            if (siguientePend != default)
                prestamo.fecha_proximo_pago = siguientePend;

            _context.Prestamos.Update(prestamo);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message  = $"Préstamo #{prestamoId} recalibrado correctamente.",
                periodosCubiertos = periodos.Count(p => p.estado_pago == 3 || p.estado_pago == 5),
                saldoActual = prestamo.saldo_actual,
            });
        }
    }
}
