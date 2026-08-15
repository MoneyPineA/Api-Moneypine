using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Tenancy
{
    // MONEYPINE-MT: los IHostedService corren por temporizador, sin request ni JWT,
    // asi que TenantResolutionMiddleware nunca les fija el tenant. Antes cada job
    // hacia Establecer(1) a mano: con un solo prestamista funcionaba, pero al dar de
    // alta el segundo esos jobs seguirian atendiendo SOLO al tenant 1 — y en silencio,
    // porque el query filter global simplemente devuelve cero filas para el resto.
    // El sintoma no seria un error sino numeros que no avanzan.
    //
    // Este helper recorre los prestamistas ACTIVOS y ejecuta el cuerpo del job una vez
    // por cada uno, con un scope propio (DbContext + ITenantContext nuevos) para que el
    // query filter apunte al tenant correcto en cada vuelta.
    public static class TenantJobRunner
    {
        // Ejecuta 'cuerpo' una vez por prestamista activo. Un fallo en un tenant se
        // registra y NO impide que se procesen los demas: si la cartera de Puebla
        // revienta, la de Michoacan tiene que barrerse igual.
        public static async Task PorCadaTenantAsync(
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            string nombreJob,
            Func<IServiceScope, int, Task> cuerpo)
        {
            List<int> tenants;
            using (var scopeInicial = scopeFactory.CreateScope())
            {
                var db = scopeInicial.ServiceProvider.GetRequiredService<AppDbContext>();
                // Prestamista NO es ITenantEntity, asi que no lo toca el query filter
                // global y se puede listar entero sin IgnoreQueryFilters.
                tenants = await db.Prestamistas
                    .Where(p => p.estatus == EstatusPrestamista.ACTIVO)
                    .Select(p => p.prestamista_id)
                    .ToListAsync();
            }

            if (tenants.Count == 0)
            {
                logger.LogWarning("{Job}: no hay prestamistas activos, nada que procesar.", nombreJob);
                return;
            }

            logger.LogInformation("{Job}: procesando {N} prestamista(s) activo(s).", nombreJob, tenants.Count);

            foreach (var tenantId in tenants)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().Establecer(tenantId);
                    await cuerpo(scope, tenantId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Job}: fallo al procesar el prestamista {TenantId}", nombreJob, tenantId);
                }
            }
        }
    }
}
