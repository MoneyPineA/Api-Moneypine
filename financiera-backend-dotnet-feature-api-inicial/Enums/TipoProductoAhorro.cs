namespace ApiEjemplo.Enums
{
    // Define si el cliente puede disponer de su dinero en cualquier momento
    // o si queda comprometido hasta la fecha de vencimiento.
    public enum TipoProductoAhorro
    {
        // Retiro libre en cualquier momento (ahorro a la vista).
        VISTA = 0,

        // El dinero queda bloqueado hasta fecha_vencimiento.
        // Un ADMIN puede autorizar un retiro anticipado; queda registrado
        // en el historial de actividades.
        PLAZO_FIJO = 1,
    }
}
