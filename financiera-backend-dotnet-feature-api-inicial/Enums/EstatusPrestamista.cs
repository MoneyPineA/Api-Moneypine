namespace ApiEjemplo.Enums
{
    // MONEYPINE-MT: estatus del tenant (prestamista). SUSPENDIDO bloquea login
    // (ver AuthController en la fase de seguridad-auth); CANCELADO es baja definitiva.
    public enum EstatusPrestamista
    {
        ACTIVO     = 0,
        SUSPENDIDO = 1,
        CANCELADO  = 2,
    }
}
