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
        /// Monto maximo de mora que un GERENTE puede condonar por si mismo.
        /// Por encima de esta cifra la operacion se convierte en una solicitud
        /// que debe autorizar un ADMIN.
        ///
        /// Referencia al definirlo: la mora promedio por credito era ~$34,700
        /// y la mayor superaba los $536,000. El tope cubre el caso corriente
        /// sin dejar los creditos grandes sin un segundo par de ojos.
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
                TipoSolicitud.ELIMINAR_PAGO
                or TipoSolicitud.CONDONAR_MORA
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
