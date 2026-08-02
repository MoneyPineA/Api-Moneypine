using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Services
{
    // MONEYPINE-FIX: hosted service que corre diariamente a medianoche UTC y
    // re-ejecuta MotorRecalculoPrestamoService.Reconstruir() sobre todos los
    // préstamos ACTIVO. Cierra el hueco donde un crédito seguía mostrando
    // ACTIVO en la BD aunque su fecha de vencimiento ya hubiera pasado, porque
    // Reconstruir() antes solo se disparaba de forma reactiva (al registrar/
    // eliminar/recalibrar un pago) y nunca por el simple paso del tiempo.
    public class EstatusPrestamoSweepService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EstatusPrestamoSweepService> _logger;
        private Timer? _timer;

        public EstatusPrestamoSweepService(
            IServiceScopeFactory scopeFactory,
            ILogger<EstatusPrestamoSweepService> logger)
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
                "EstatusPrestamoSweepService: primera ejecución en {h:F1} horas (medianoche UTC)",
                dueTime.TotalHours);

            _timer = new Timer(DoWork, null, dueTime, TimeSpan.FromDays(1));
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("EstatusPrestamoSweepService: iniciando barrido diario de estatus");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var candidatos = await db.Prestamos
                    .Where(p => p.estatus == EstatusPrestamo.ACTIVO)
                    .Select(p => p.prestamo_id)
                    .ToListAsync();

                int actualizados = 0;
                foreach (var prestamoId in candidatos)
                {
                    try
                    {
                        // Cada préstamo se reconstruye con su propio scope/DbContext
                        // para que un error puntual no invalide el resto del barrido
                        // ni deje el ChangeTracker en un estado inconsistente.
                        using var pScope = _scopeFactory.CreateScope();
                        var motor = pScope.ServiceProvider.GetRequiredService<MotorRecalculoPrestamoService>();
                        await motor.Reconstruir(prestamoId);
                        actualizados++;
                    }
                    catch (Exception exPrestamo)
                    {
                        _logger.LogError(exPrestamo,
                            "EstatusPrestamoSweepService: error al reconstruir préstamo {id}", prestamoId);
                    }
                }

                _logger.LogInformation(
                    "EstatusPrestamoSweepService: {n} préstamo(s) ACTIVO revisado(s).", actualizados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EstatusPrestamoSweepService: error durante el barrido diario");
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
