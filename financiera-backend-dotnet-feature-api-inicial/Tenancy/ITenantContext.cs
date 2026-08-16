namespace ApiEjemplo.Tenancy
{
    // MONEYPINE-MT: contexto de tenant, scoped por request (Fase 1 — Parte 4.5).
    // Lo resuelve TenantResolutionMiddleware a partir del claim JWT y lo lee
    // AppDbContext como propiedades de INSTANCIA (nunca como constante capturada
    // en el query filter — ver advertencia en AppDbContext.cs).
    public interface ITenantContext
    {
        int PrestamistaId { get; }
        bool EsPlataforma { get; }
        void Establecer(int prestamistaId, bool esPlataforma = false);
    }

    // MONEYPINE-MT: implementación scoped. Antes de que el middleware corra
    // (o para requests anónimos, p.ej. /api/auth/login) PrestamistaId queda en 0
    // y EsPlataforma en false — el query filter con TenantId==0 no matchea
    // ninguna fila real, así que no hay fuga por default.
    public class TenantContext : ITenantContext
    {
        public int PrestamistaId { get; private set; }
        public bool EsPlataforma { get; private set; }

        public void Establecer(int prestamistaId, bool esPlataforma = false)
        {
            PrestamistaId = prestamistaId;
            EsPlataforma = esPlataforma;
        }
    }
}
