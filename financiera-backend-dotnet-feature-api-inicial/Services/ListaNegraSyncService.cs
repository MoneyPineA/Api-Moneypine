using ApiEjemplo.Data;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Services
{
    /// <summary>
    /// Sincroniza la lista negra todos los dias, junto al resto de procesos
    /// nocturnos.
    ///
    /// Antes dependia de que alguien entrara a la pantalla y pulsara
    /// "Sincronizar". Cuando se reviso, la ultima corrida llevaba tanto tiempo
    /// sin ejecutarse que quedaban 158 clientes por dar de alta y los dias de
    /// mora registrados estaban a anos de la realidad: un cliente figuraba con
    /// 174 dias cuando ya acumulaba 1201.
    ///
    /// Corre despues de medianoche a proposito: la mora la recalcula
    /// EstatusPrestamoSweepService a esa hora, y sincronizar antes usaria cifras
    /// del dia anterior.
    ///
    /// Las bajas manuales se respetan (bloquea_reingreso_auto): este proceso
    /// nunca revierte la decision de un administrador.
    /// </summary>
    public class ListaNegraSyncService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ListaNegraSyncService> _logger;
        private Timer? _timer;

        // Media hora despues de medianoche, para no pisarse con el barrido de
        // estatus que corre a las 00:00.
        private static readonly TimeSpan HoraDeCorte = TimeSpan.FromMinutes(30);

        public ListaNegraSyncService(IServiceProvider services, ILogger<ListaNegraSyncService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken ct)
        {
            var ahora = DateTime.UtcNow;
            var proximaCorrida = ahora.Date.AddDays(1).Add(HoraDeCorte);
            var espera = proximaCorrida - ahora;

            _logger.LogInformation(
                "ListaNegraSyncService: primera ejecución en {h:F1} horas.", espera.TotalHours);

            _timer = new Timer(async _ => await EjecutarAsync(), null, espera, TimeSpan.FromDays(1));
            return Task.CompletedTask;
        }

        private async Task EjecutarAsync()
        {
            try
            {
                // MONEYPINE-MT: la lista negra es de cada prestamista. Fijado al tenant 1,
                // la de cualquier otro no se sincronizaba nunca.
                await TenantJobRunner.PorCadaTenantAsync(
                    _services.GetRequiredService<IServiceScopeFactory>(), _logger, "ListaNegraSyncService",
                    async (scope, tenantId) =>
                    {
                        var svc = scope.ServiceProvider.GetRequiredService<ListaNegraService>();

                        // usuarioId null: la ejecuto el sistema, no una persona.
                        var r = await svc.SincronizarAsync(null);

                        _logger.LogInformation(
                            "ListaNegraSyncService: prestamista {t} — {ag} agregados, {rem} removidos, " +
                            "{om} omitidos por baja manual.",
                            tenantId, r.agregados, r.removidos, r.omitidos_por_baja_manual);
                    });
            }
            catch (Exception ex)
            {
                // Un fallo aqui no debe tumbar el proceso: se reintenta manana.
                _logger.LogError(ex, "ListaNegraSyncService: error durante la sincronización diaria");
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
