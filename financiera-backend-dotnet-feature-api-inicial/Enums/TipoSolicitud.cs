namespace ApiEjemplo.Enums
{
    /// <summary>
    /// Acciones que un rol distinto de ADMIN no puede ejecutar por si mismo y
    /// debe pedir autorizacion. Todas afectan calculos de negocio o el
    /// historial crediticio del cliente.
    ///
    /// Se guarda como string en la BD: agregar un valor no rompe los
    /// registros existentes ni obliga a respetar el orden.
    /// </summary>
    public enum TipoSolicitud
    {
        ELIMINAR_PAGO,
        CONDONAR_MORA,
        QUITAR_LISTA_NEGRA,
        QUITAR_BURO,
        ELIMINAR_CREDITO,
        ELIMINAR_CLIENTE,

        /// <summary>
        /// Baja de un trabajador. Nunca se borra el registro: se pasa a
        /// INACTIVO. Los pagos guardan cobrador_id y las actividades guardan
        /// UserId; borrarlo dejaria huerfano el historial de quien cobro que.
        /// </summary>
        DAR_BAJA_TRABAJADOR,
    }

    public enum EstadoSolicitud
    {
        PENDIENTE,
        APROBADA,
        RECHAZADA,

        /// <summary>
        /// El admin intento aprobarla pero el mundo ya habia cambiado: el pago
        /// se elimino por otra via, el credito se liquido, la mora ya no existe.
        /// Se marca asi en vez de ejecutar a ciegas sobre un estado distinto
        /// al que el solicitante describio.
        /// </summary>
        OBSOLETA,

        /// <summary>El propio solicitante la retiro antes de que la resolvieran.</summary>
        CANCELADA,
    }
}
