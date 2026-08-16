namespace ApiEjemplo.Tenancy
{
    // MONEYPINE-MT: toda entidad de negocio implementa esta interfaz para llevar
    // prestamista_id. El query filter global (HasQueryFilter) NO se agrega aquí
    // — es responsabilidad de mt-core-tenancy en Data/AppDbContext.cs.
    public interface ITenantEntity
    {
        int prestamista_id { get; set; }
    }
}
