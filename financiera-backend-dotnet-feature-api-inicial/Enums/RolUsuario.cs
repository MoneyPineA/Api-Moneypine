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

        // MONEYPINE-MT: Fase 3 — panel de plataforma. Se persiste como STRING
        // (Data/AppDbContext.cs: entity.Property(u => u.rol).HasConversion<string>()),
        // así que agregarlo al final es seguro: no hay ordinal que se corrompa.
        // Verificado contra la BD local: la columna usuario.rol es LONGTEXT, no
        // un entero, así que ningún registro existente depende del orden del enum.
        PLATFORM_ADMIN
    }
}