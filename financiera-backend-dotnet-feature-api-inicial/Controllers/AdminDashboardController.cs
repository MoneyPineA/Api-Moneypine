using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.DTOs.Dashboard;
using Microsoft.AspNetCore.Authorization;
using ApiEjemplo.Helpers;
using ApiEjemplo.Models;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/admin/dashboard")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminDashboardController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // GET: api/admin/dashboard/Pagos-Totales
        // Retorna pagos agrupados por mes
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpGet("Pagos-Totales")]
        public async Task<IActionResult> GetPagosTotales(
            PeriodoDashboard period = PeriodoDashboard.day,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            // ==========================
            // VALORES POR DEFECTO
            // ==========================

            var end = endDate.HasValue
                ? TimeHelper.ConvertToMexicoTime(DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc))
                : TimeHelper.GetMexicoTime();
            var start = startDate.HasValue
                ? TimeHelper.ConvertToMexicoTime(DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc))
                : end.AddDays(-30);

            end = end.Date.AddDays(1).AddTicks(-1);

            // ==========================
            // QUERY BASE
            // ==========================

            var query = _context.Pagos
                .Where(p =>
                    p.estatus == EstatusPago.APLICADO &&
                    p.fecha_pago >= start &&
                    p.fecha_pago <= end);

            List<PagosTotalesItemDTO> data;

            // ==========================
            // AGRUPACIÓN
            // ==========================

            if (period == PeriodoDashboard.year)
            {
                var result = await query
                    .GroupBy(p => p.fecha_pago.Year)
                    .Select(g => new
                    {
                        Year = g.Key,
                        total = g.Sum(p => p.monto_pagado)
                    })
                    .OrderBy(x => x.Year)
                    .ToListAsync();

                data = result.Select(x => new PagosTotalesItemDTO
                {
                    date = x.Year.ToString(),
                    total = x.total
                }).ToList();
            }
            else if (period == PeriodoDashboard.month)
            {
                var result = await query
                    .GroupBy(p => new { p.fecha_pago.Year, p.fecha_pago.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        total = g.Sum(p => p.monto_pagado)
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToListAsync();

                data = result.Select(x => new PagosTotalesItemDTO
                {
                    date = $"{x.Year}-{x.Month:D2}",
                    total = x.total
                }).ToList();
            }
            else
            {
                var result = await query
                    .GroupBy(p => p.fecha_pago.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        total = g.Sum(p => p.monto_pagado)
                    })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                data = result.Select(x => new PagosTotalesItemDTO
                {
                    date = x.date.ToString("yyyy-MM-dd"),
                    total = x.total
                }).ToList();
            }

            var response = new PagosTotalesResponseDTO
            {
                period = period.ToString(),
                data = data
            };

            return Ok(response);
        }

        // =====================================================
        // GET: api/admin/dashboard/financial-summary
        // Retorna métricas financieras generales del sistema
        // =====================================================
        // =====================================================
        // GET: api/admin/dashboard/indicadores
        // Retorna indicadores de cartera para CreditPortfolio
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpGet("indicadores")]
        public async Task<IActionResult> GetIndicadores()
        {
            var activos   = await _context.Prestamos.Where(p => p.estatus == EstatusPrestamo.ACTIVO).ToListAsync();
            var atrasados = await _context.Prestamos.Where(p => p.estatus == EstatusPrestamo.ATRASADO).ToListAsync();

            // MONEYPINE-FIX: moratoriosGenerados = mora_diaria × días_vencido por cada periodo pendiente vencido
            // BUG ANTERIOR: sumaba pago_pactado (capital+interés completo) en lugar de mora real → número inflado al 93% del capital
            // interes_moratorio en periodos estado_pago=1 siempre es 0 (solo se guarda al pagar)
            var hoy = TimeHelper.GetMexicoTime().Date;
            var periodsOverdue = await _context.PeriodosAmortizacion
                .Where(pa => pa.estado_pago == 1 && pa.fecha_vencimiento <= hoy)
                .Join(_context.Prestamos,
                      pa => pa.prestamo_id,
                      pr => pr.prestamo_id,
                      (pa, pr) => new { pa.fecha_vencimiento, pr.mora_diaria })
                .ToListAsync();

            decimal moratoriosGenerados = periodsOverdue.Sum(x =>
            {
                int dias = Math.Max(0, (hoy - x.fecha_vencimiento.Date).Days);
                return Math.Round(x.mora_diaria * dias, 2);
            });

            return Ok(new
            {
                creditosActivos     = activos.Count + atrasados.Count, // MONEYPINE-FIX: incluye ATRASADO para coincidir con tabla
                capitalActual       = activos.Sum(p => p.saldo_actual), // MONEYPINE-FIX: solo cartera sana (ACTIVO)
                interesActual       = activos.Sum(p => p.saldo_actual * p.tasa_interes / 100)               // MONEYPINE-FIX: interés sobre saldo pendiente real
                                    + atrasados.Sum(p => p.saldo_actual * p.tasa_interes / 100),
                totalCartera        = activos.Sum(p => p.saldo_actual) + atrasados.Sum(p => p.saldo_actual), // MONEYPINE-FIX: usa saldo_actual, no capital original
                carteraCorriente    = activos.Sum(p => p.saldo_actual),
                saldoEnAtraso       = atrasados.Sum(p => p.saldo_actual),
                moratoriosGenerados
            });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet("financial-summary")]
        public async Task<IActionResult> GetFinancialSummary()
        {
            var summary = await _context.Prestamos
                .Where(p => p.estatus == EstatusPrestamo.ACTIVO ||
                            p.estatus == EstatusPrestamo.ATRASADO)
                .GroupBy(p => 1)
                .Select(g => new DashboardFinancialSummaryDTO
                {
                    cartera_total = g.Sum(p => p.monto),
                    capital_actual = g.Sum(p => p.saldo_actual),
                    interes_total = g.Sum(p => p.monto_total - p.monto),
                    numero_total_creditos = g.Count()
                })
                .FirstOrDefaultAsync();

            if (summary == null)
            {
                summary = new DashboardFinancialSummaryDTO
                {
                    cartera_total = 0,
                    capital_actual = 0,
                    interes_total = 0,
                    numero_total_creditos = 0
                };
            }

            return Ok(summary);
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivity()
        {
            var now = TimeHelper.GetMexicoTime();

            // DETECTAR PRÉSTAMOS ATRASADOS AUTOMÁTICAMENTE
            var prestamosAtrasados = await _context.Prestamos
                .Where(p =>
                    p.estatus == EstatusPrestamo.ACTIVO &&
                    p.fecha_proximo_pago != null &&
                    now > p.fecha_proximo_pago.Value.AddDays(p.dias_gracia))
                .ToListAsync();

            foreach (var prestamo in prestamosAtrasados)
            {
                bool yaExiste = await _context.ActivityLogs.AnyAsync(a =>
                    a.Type == ActivityType.PAYMENT_OVERDUE &&
                    a.ClientId == prestamo.cliente_id &&
                    a.CreatedAt.Date == now.Date
                );

                if (!yaExiste)
                {
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Type        = ActivityType.PAYMENT_OVERDUE,
                        ClientId    = prestamo.cliente_id,
                        Amount      = prestamo.saldo_actual,
                        Priority    = NotificationLevel.HIGH,
                        Description = $"Pago vencido en cr\u00e9dito #{prestamo.prestamo_id} — ${prestamo.saldo_actual:N2} en atraso"
                    });
                }
            }

            await _context.SaveChangesAsync();

            // MONEYPINE-FIX: excluir PAYMENT_OVERDUE de clientes sin ningún préstamo activo
            var clientesConPrestamoActivo = await _context.Prestamos
                .Where(p => p.estatus != EstatusPrestamo.LIQUIDADO)
                .Select(p => p.cliente_id)
                .Distinct()
                .ToListAsync();

            var logs = await _context.ActivityLogs
                .Where(a =>
                    a.Type != ActivityType.PAYMENT_OVERDUE ||
                    clientesConPrestamoActivo.Contains(a.ClientId))
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync();

        var activities = logs.Select(a => new RecentActivityDTO
        {
            Type = a.Type.ToString(),
            Label = a.Type switch
            {
                ActivityType.PAYMENT_RECEIVED => "Pago recibido",
                ActivityType.CREDIT_APPROVED => "Crédito aprobado",
                ActivityType.PAYMENT_OVERDUE => "Pago vencido",
                _ => "Actividad"
            },
            ClientId = a.ClientId,
            Amount = a.Amount,
            Priority = a.Priority,
            Color = a.Priority switch
            {
                NotificationLevel.HIGH => "red",
                NotificationLevel.NEUTRAL => "yellow",
                NotificationLevel.POSITIVE => "green",
                _ => "gray"
            },
            CreatedAt = a.CreatedAt
        }).ToList();

            return Ok(new { results = activities });
        }
    }
}