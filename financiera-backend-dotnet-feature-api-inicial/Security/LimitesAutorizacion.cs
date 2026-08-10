using ApiEjemplo.Enums;

namespace ApiEjemplo.Security
{
    /// <summary>
    /// Topes de monto por rol para acciones que afectan calculos de negocio.
    ///
    /// Estan aqui y no dispersos en los controladores para que el dia que
    /// cambien no haya que buscarlos: el limite es una regla de negocio, no
    /// un detalle de un endpoint.
    /// </summary>
    public static class LimitesAutorizacion
    {
        /// <summary>
        /// MONEYPINE-FIX: condonar mora (parcial o total) quedó restringido a
        /// solo ADMIN — ya no hay tope de GERENTE ni solicitud de por medio,
        /// ver PrestamoController.CondonarMora. Se deja la constante (no la
        /// referencia nadie más) por si se retoma un tope por rol más adelante.
        /// </summary>
        public const decimal CondonacionMoraGerente = 50_000m;

        /// <summary>
        /// Que puede PEDIR cada rol. Poder solicitar no es poder ejecutar: todo
        /// lo de aqui pasa igual por la aprobacion de un ADMIN.
        ///
        /// Las bajas de credito, cliente y trabajador quedan fuera del alcance
        /// del COBRADOR: su trabajo es cobrar en calle, no dar de baja
        /// entidades del sistema.
        /// </summary>
        public static bool PuedeSolicitar(RolUsuario rol, TipoSolicitud tipo)
        {
            if (rol == RolUsuario.ADMIN) return false; // ejecuta directo, no solicita
            if (rol == RolUsuario.CLIENTE) return false;

            return tipo switch
            {
                // MONEYPINE-FIX: condonar mora ya no es solicitable — quedó
                // restringido a que solo un ADMIN la ejecute directamente.
                TipoSolicitud.CONDONAR_MORA => false,

                TipoSolicitud.ELIMINAR_PAGO
                or TipoSolicitud.QUITAR_LISTA_NEGRA
                or TipoSolicitud.QUITAR_BURO
                    => rol is RolUsuario.GERENTE or RolUsuario.RECURSOS_HUMANOS
                           or RolUsuario.COBRADOR or RolUsuario.DIRECCION_GENERAL,

                TipoSolicitud.ELIMINAR_CREDITO
                or TipoSolicitud.ELIMINAR_CLIENTE
                or TipoSolicitud.DAR_BAJA_TRABAJADOR
                    => rol is RolUsuario.GERENTE or RolUsuario.RECURSOS_HUMANOS
                           or RolUsuario.DIRECCION_GENERAL,

                _ => false,
            };
        }
    }
}
