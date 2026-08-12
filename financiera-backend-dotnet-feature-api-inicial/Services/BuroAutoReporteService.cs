using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Services
{
    // MONEYPINE-FIX: hosted service que corre diariamente a medianoche UTC
    // y auto-reporta al buró los préstamos con mora >= 90 días,
    // solo si reporte_automatico = 'true' en configuracion_sistema.
    public class BuroAutoReporteService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BuroAutoReporteService> _logger;
        private Timer? _timer;

        public BuroAutoReporteService(
            IServiceScopeFactory scopeFactory,
            ILogger<BuroAutoReporteService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var dueTime = nextMidnight - now;

            _logger.LogInformation(
                "BuroAutoReporteService: primera ejecución en {h:F1} horas (medianoche UTC)",
                dueTime.TotalHours);

            _timer = new Timer(DoWork, null, dueTime, TimeSpan.FromDays(1));
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("BuroAutoReporteService: iniciando revisión diaria de mora");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // MONEYPINE-MT: este timer no pasa por TenantResolutionMiddleware (no hay
                // request/JWT), así que ITenantContext quedaría en PrestamistaId=0 y el
                // query filter global dejaría db.Prestamos/db.BuroExclusiones/db.BuroAutoReportes
                // vacíos — el job "correría" sin procesar nada, en silencio. Fase 1 solo
                // tiene el tenant 1; se fija explícito para no regresar el comportamiento
                // actual. DEUDA: cuando exista un segundo tenant real, este job debe
                // iterar sobre cada prestamista activo (Fase 3/4), no asumir el 1.
                scope.ServiceProvider.GetRequiredService<ITenantContext>().Establecer(1);

                // Leer configuracion_sistema para verificar si reporte automático está activo
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                string reporteActivo = "false";
                using (var cmd = conn.CreateCommand())
                {
                    // MONEYPINE-MT: filtro manual, SQL crudo. Sin el tenant explicito,
                    // el flag de un prestamista decidiria el auto-reporte a buro de
                    // todos los demas. El scope ya fijo el tenant unas lineas arriba.
                    cmd.CommandText =
                        "SELECT valor FROM configuracion_sistema " +
                        "WHERE clave = 'reporte_automatico' AND prestamista_id = 1";
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                        reporteActivo = reader.GetString(0);
                }

                if (reporteActivo != "true")
                {
                    _logger.LogInformation("BuroAutoReporteService: reporte_automatico = false, omitiendo.");
                    return;
                }

                var hoy = DateTime.UtcNow.Date;

                var excluidos = (await db.BuroExclusiones
                    .Select(e => e.cliente_id)
                    .ToListAsync())
                    .ToHashSet();

                var candidatos = await db.Prestamos
                    .Where(p =>
                        (p.estatus == EstatusPrestamo.ACTIVO || p.estatus == EstatusPrestamo.ATRASADO)
                        && p.fecha_proximo_pago.HasValue
                        && !excluidos.Contains(p.cliente_id))
                    .ToListAsync();

                int reportados = 0;
                foreach (var p in candidatos)
                {
                    var diasMora = (int)(hoy - p.fecha_proximo_pago!.Value.Date).TotalDays - p.dias_gracia;
                    if (diasMora < 90) continue;

                    var existing = await db.BuroAutoReportes.FirstOrDefaultAsync(b => b.cliente_id == p.cliente_id);
                    if (existing != null)
                    {
                        existing.prestamo_id    = p.prestamo_id;
                        existing.fecha_reporte  = DateTime.UtcNow;
                        existing.dias_mora      = diasMora;
                        existing.saldo_pendiente = p.saldo_actual;
                        existing.motivo         = $"AUTO-REPORTADO: {diasMora} días de mora";
                    }
                    else
                    {
                        db.BuroAutoReportes.Add(new BuroAutoReporte
                        {
                            cliente_id      = p.cliente_id,
                            prestamo_id     = p.prestamo_id,
                            fecha_reporte   = DateTime.UtcNow,
                            dias_mora       = diasMora,
                            saldo_pendiente = p.saldo_actual,
                            motivo          = $"AUTO-REPORTADO: {diasMora} días de mora",
                        });
                    }
                    reportados++;
                }

                await db.SaveChangesAsync();
                _logger.LogInformation("BuroAutoReporteService: {n} cliente(s) procesados con mora >= 90 días.", reportados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BuroAutoReporteService: error durante la revisión diaria");
            }
        }

        public Task StopAsync(CancellationToken ct)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose() => _timer?.Dispose();
    }
}
