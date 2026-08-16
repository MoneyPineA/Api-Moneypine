namespace ApiEjemplo.Enums
{
    public enum RolUsuario
    {
        CLIENTE,
        ADMIN,
        COBRADOR,
        GERENTE,
        DIRECCION_GENERAL,
        RECURSOS_HUMANOS,

        // MONEYPINE: rol CONTADOR (2026-08-14). Por decision del negocio es un CLON
        // de DIRECCION_GENERAL: ve y puede lo mismo (todo menos Configuracion). Se
        // persiste como STRING igual que el resto, asi que agregarlo aqui es seguro.
        // Si alguna vez debe separarse de Direccion General, hay que tocar los mismos
        // puntos donde hoy aparecen JUNTOS: RolesResumen/RolesReportes
        // (AdminDashboardController), LimitesAutorizacion, y en el front MODULE_ACCESS
        // + CreditoDetalle.puedeEditar.
        CONTADOR,

        // MONEYPINE-MT: Fase 3 — panel de plataforma. Se persiste como STRING
        // (Data/AppDbContext.cs: entity.Property(u => u.rol).HasConversion<string>()),
        // así que agregarlo al final es seguro: no hay ordinal que se corrompa.
        // Verificado contra la BD local: la columna usuario.rol es LONGTEXT, no
        // un entero, así que ningún registro existente depende del orden del enum.
        PLATFORM_ADMIN
    }
}