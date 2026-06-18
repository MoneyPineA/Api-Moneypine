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
        // Registra un pago usando el motor unificado
        // =====================================================
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PagoCreateDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            // Calcular distribución (lectura, sin efectos secundarios)
            var dist = await _motorPago.CalcularDistribucion(dto);
            if (!dist.ok)
                return BadRequest(dist.error);

            // Cargar entidades CON tracking para aplicar cambios
            var prestamo = await _context.Prestamos.FirstOrDefaultAsync(p => p.prestamo_id == dto.prestamo_id);
            if (prestamo == null) return BadRequest("El préstamo no existe");

            DateTime fechaPago = dto.fecha_pago.HasValue
                ? DateTime.SpecifyKind(dto.fecha_pago.Value.Date, DateTimeKind.Unspecified)
                : TimeHelper.GetMexicoTime();

            // ── Aplicar cambios en periodos ───────────────────────────────

            foreach (var det in dist.detalles)
            {
                if (det.periodo_id == 0) continue;

                var periodo = await _context.PeriodosAmortizacion.FindAsync(det.periodo_id);
                if (periodo == null) continue;

                if (det.periodo_cerrado)
                {
                    periodo.estado_pago      = det.nuevo_estado;
                    periodo.fecha_pagado     = fechaPago;
                    periodo.dias_moratorio   = det.dias_moratorio;
                    periodo.interes_moratorio = det.interes_moratorio;
                }
                else if (det.delta_ahorro > 0)
                {
                    periodo.ahorro_por_pago += det.delta_ahorro;
                }
                _context.PeriodosAmortizacion.Update(periodo);
            }

            // ── Actualizar saldo y estatus del préstamo ───────────────────

            prestamo.saldo_actual = dist.saldo_nuevo;

            // fecha_proximo_pago: siguiente periodo pendiente (estado=1)
            var siguientePendiente = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .FirstOrDefaultAsync();

            if (siguientePendiente != null)
            {
                prestamo.fecha_proximo_pago = siguientePendiente.fecha_vencimiento;
                if (prestamo.estatus == EstatusPrestamo.ATRASADO)
                    prestamo.estatus = EstatusPrestamo.ACTIVO;
            }

            if (prestamo.saldo_actual <= 0)
            {
                prestamo.estatus  = EstatusPrestamo.LIQUIDADO;
                prestamo.fecha_fin = fechaPago;
            }
            else if (dist.mora_total > 0 && dist.capital_total == 0)
            {
                prestamo.estatus = EstatusPrestamo.ATRASADO;
            }

            // ── Crear registro de pago ────────────────────────────────────

            int? usuarioId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var pid))
                usuarioId = pid;

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
            _context.Prestamos.Update(prestamo);
            await _context.SaveChangesAsync(); // pago.pago_id ahora disponible

            // ── Crear registros PagoDetalle ───────────────────────────────
            foreach (var det in dist.detalles)
            {
                _context.PagoDetalles.Add(new PagoDetalle
                {
                    pago_id          = pago.pago_id,
                    periodo_id       = det.periodo_id > 0 ? det.periodo_id : null,
                    capital_aplicado  = det.capital_aplicado,
                    interes_aplicado  = det.interes_aplicado,
                    iva_aplicado      = det.iva_aplicado,
                    mora_aplicada     = det.mora_aplicada,
                    periodo_cerrado   = det.periodo_cerrado,
                    tipo_pago         = dto.tipo_pago,
                });
            }
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // ── Actividad y notificación ──────────────────────────────────

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
                dist.capital_total + dist.interes_total + dist.mora_total,
                NotificationLevel.POSITIVE,
                description: $"Pago de ${dto.monto_pagado:N2} ({dto.tipo_pago}) al crédito #{prestamo.prestamo_id} del cliente {nombreCliente}",
                userId: usuarioId);

            var msg = prestamo.estatus == EstatusPrestamo.LIQUIDADO
                ? $"Préstamo #{prestamo.prestamo_id} liquidado completamente"
                : $"Pago registrado: ${dto.monto_pagado:N2} al préstamo #{prestamo.prestamo_id}";
            var lvl = prestamo.estatus == EstatusPrestamo.LIQUIDADO
                ? NotificationLevel.POSITIVE : NotificationLevel.NEUTRAL;
            await _notificationService.CreateNotification(1, msg, lvl);

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
        // Usa PagoDetalle cuando existe (pagos nuevos); cae en lógica
        // legada basada en fecha_pagado para pagos históricos.
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

            var detalles = await _context.PagoDetalles
                .Where(pd => pd.pago_id == id)
                .ToListAsync();

            var hoy = TimeHelper.GetMexicoTime().Date;

            if (detalles.Any())
            {
                // ── Camino nuevo: usar pago_detalle para reversión exacta ──

                decimal saldoRevertir = 0m;

                foreach (var det in detalles)
                {
                    if (det.periodo_id == null) continue;
                    var periodo = await _context.PeriodosAmortizacion.FindAsync(det.periodo_id.Value);
                    if (periodo == null) continue;

                    if (det.periodo_cerrado)
                    {
                        // Revertir el cierre del periodo
                        periodo.estado_pago  = 1;
                        periodo.fecha_pagado = null;
                        int diasMora = Math.Max(0, (int)(hoy - periodo.fecha_vencimiento.Date).TotalDays);
                        periodo.dias_moratorio    = diasMora;
                        periodo.interes_moratorio = diasMora > 0 && prestamo.mora_diaria > 0
                            ? Math.Round(prestamo.mora_diaria * diasMora, 2)
                            : 0m;
                        saldoRevertir += det.capital_aplicado;
                    }
                    else if (det.mora_aplicada > 0)
                    {
                        // Revertir ahorro_por_pago (solo_mora o mora parcial)
                        periodo.ahorro_por_pago = Math.Max(0m, periodo.ahorro_por_pago - det.mora_aplicada);
                    }
                    _context.PeriodosAmortizacion.Update(periodo);
                }

                prestamo.saldo_actual += saldoRevertir;

                // solo_capital parcial (periodo no cerrado, capital aplicado, sin mora)
                var capSinCierre = detalles
                    .Where(d => !d.periodo_cerrado && d.mora_aplicada == 0 && d.capital_aplicado > 0)
                    .Sum(d => d.capital_aplicado);
                prestamo.saldo_actual += capSinCierre;

                // Restaurar fecha_proximo_pago al vencimiento más antiguo revertido
                var vencimientosRevertidos = new List<DateTime>();
                foreach (var det in detalles.Where(d => d.periodo_cerrado && d.periodo_id != null))
                {
                    var periodo = await _context.PeriodosAmortizacion.FindAsync(det.periodo_id!.Value);
                    if (periodo != null)
                        vencimientosRevertidos.Add(periodo.fecha_vencimiento);
                }
                if (vencimientosRevertidos.Any())
                    prestamo.fecha_proximo_pago = vencimientosRevertidos.Min();

                if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                    prestamo.estatus = EstatusPrestamo.ATRASADO;
            }
            else
            {
                // ── Camino legado: búsqueda por fecha_pagado ──────────────

                if (pago.abono_capital == 0 && pago.mora_pagada > 0)
                {
                    // solo_mora legado
                    decimal moraADescontar = pago.mora_pagada;
                    var congelados = await _context.PeriodosAmortizacion
                        .Where(pa => pa.prestamo_id == pago.prestamo_id && pa.estado_pago == 5 && pa.ahorro_por_pago > 0)
                        .OrderBy(pa => pa.periodo).ToListAsync();
                    foreach (var p in congelados)
                    {
                        if (moraADescontar <= 0) break;
                        decimal red = Math.Min(p.ahorro_por_pago, moraADescontar);
                        p.ahorro_por_pago -= red; moraADescontar -= red;
                        _context.PeriodosAmortizacion.Update(p);
                    }
                    if (moraADescontar > 0)
                    {
                        var retraso = await _context.PeriodosAmortizacion
                            .Where(pa => pa.prestamo_id == pago.prestamo_id && pa.estado_pago == 1 && pa.ahorro_por_pago > 0)
                            .OrderBy(pa => pa.periodo).ToListAsync();
                        foreach (var p in retraso)
                        {
                            if (moraADescontar <= 0) break;
                            decimal red = Math.Min(p.ahorro_por_pago, moraADescontar);
                            p.ahorro_por_pago -= red; moraADescontar -= red;
                            _context.PeriodosAmortizacion.Update(p);
                        }
                    }
                }
                else
                {
                    var periodosARevertir = await _context.PeriodosAmortizacion
                        .Where(pa => pa.prestamo_id == pago.prestamo_id
                                  && (pa.estado_pago == 2 || pa.estado_pago == 3 || pa.estado_pago == 5)
                                  && pa.fecha_pagado.HasValue
                                  && pa.fecha_pagado.Value.Date == pago.fecha_pago.Date)
                        .OrderBy(pa => pa.periodo).ToListAsync();

                    DateTime? fppRevertida = null;
                    foreach (var p in periodosARevertir)
                    {
                        p.estado_pago  = 1;
                        p.fecha_pagado = null;
                        int dias = Math.Max(0, (int)(hoy - p.fecha_vencimiento.Date).TotalDays);
                        p.dias_moratorio    = dias;
                        p.interes_moratorio = dias > 0 && prestamo.mora_diaria > 0
                            ? Math.Round(prestamo.mora_diaria * dias, 2) : 0m;
                        _context.PeriodosAmortizacion.Update(p);
                        if (fppRevertida == null || p.fecha_vencimiento < fppRevertida)
                            fppRevertida = p.fecha_vencimiento;
                    }
                    if (fppRevertida.HasValue)
                        prestamo.fecha_proximo_pago = fppRevertida;

                    decimal saldoDB = await _context.PeriodosAmortizacion
                        .Where(pa => pa.prestamo_id == pago.prestamo_id && pa.estado_pago == 1)
                        .SumAsync(pa => pa.abono_capital);
                    prestamo.saldo_actual = saldoDB + periodosARevertir.Sum(p => p.abono_capital);

                    if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                        prestamo.estatus = EstatusPrestamo.ATRASADO;
                }
            }

            _context.Pagos.Remove(pago);
            _context.Prestamos.Update(prestamo);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }
    }
}
