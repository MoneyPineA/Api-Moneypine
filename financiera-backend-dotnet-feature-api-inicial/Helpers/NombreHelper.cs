namespace ApiEjemplo.Helpers
{
    public static class NombreHelper
    {
        // MONEYPINE-FIX: construye nombre del cliente sin duplicar apellido materno en clientes legacy
        // donde Usuario.nombre ya puede contener el nombre completo (ej. "ROBERTO CHAVEZ ROJAS")
        public static string BuildNombreCliente(string? nombre, string? apellido, string? apellidoMaterno, int clienteId)
        {
            var nombreBase = $"{nombre ?? ""} {apellido ?? ""}".Trim();
            var apMat      = (apellidoMaterno ?? "").Trim();

            string nombreFinal;
            if (string.IsNullOrWhiteSpace(apMat))
                nombreFinal = nombreBase;
            else if (nombreBase.ToUpper().EndsWith(apMat.ToUpper()))
                nombreFinal = nombreBase;           // apellido_materno ya está incluido
            else
                nombreFinal = $"{nombreBase} {apMat}".Trim();

            return string.IsNullOrWhiteSpace(nombreFinal) ? $"Cliente #{clienteId}" : nombreFinal;
        }
    }
}
