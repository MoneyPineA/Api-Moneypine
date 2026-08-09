using ApiEjemplo.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Security.Claims;

namespace ApiEjemplo.Security
{
    /// <summary>
    /// Convierte los 403 de autorizacion en una respuesta que una persona pueda
    /// entender.
    ///
    /// Por defecto, cuando [Authorize(Roles="ADMIN")] rechaza a alguien,
    /// ASP.NET devuelve 403 con el cuerpo VACIO. El frontend no recibe nada que
    /// mostrar y el usuario acaba viendo "Error 403", que no explica que pasó
    /// ni que puede hacer.
    ///
    /// Aqui se responde con un mensaje en espanol, el rol del usuario y —cuando
    /// la accion se puede pedir— la indicacion de que existe el camino de
    /// solicitud. Aplica a TODOS los endpoints de una vez, sin tocarlos uno a uno.
    /// </summary>
    public class RespuestaPermisoHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _predeterminado = new();

        /// <summary>
        /// Acciones que el usuario puede pedir por SolicitudAprobacion. La clave
        /// es un fragmento de la ruta; el valor, como nombrar la accion al
        /// usuario. Si la ruta no esta aqui se usa un mensaje generico.
        /// </summary>
        private static readonly (string Ruta, string Metodo, string Accion)[] Solicitables =
        {
            ("/api/pago",             "DELETE", "eliminar un pago"),
            ("/api/prestamo",         "DELETE", "dar de baja un crédito"),
            ("/api/cliente",          "DELETE", "dar de baja un cliente"),
            ("/api/usuario",          "DELETE", "dar de baja a un trabajador"),
            ("/api/listanegra",       "PUT",    "quitar a un cliente de la lista negra"),
            ("/api/listanegra",       "POST",   "agregar a un cliente a la lista negra"),
            ("/api/buroexclusion",    "DELETE", "quitar a un cliente del buró"),
            ("/api/buroautoreporte",  "DELETE", "quitar el auto-reporte de buró"),
        };

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            // Solo se personaliza el "estas autenticado pero no te alcanza el
            // rol". El 401 (sin sesion) lo sigue manejando el flujo normal, que
            // el frontend ya usa para renovar el token o mandar al login.
            if (authorizeResult.Forbidden && context.User?.Identity?.IsAuthenticated == true)
            {
                var rol = context.User.FindFirst(ClaimTypes.Role)?.Value ?? "tu rol";
                var ruta = context.Request.Path.Value?.ToLowerInvariant() ?? "";
                var metodo = context.Request.Method;

                var accion = Solicitables
                    .FirstOrDefault(s => ruta.StartsWith(s.Ruta) && s.Metodo == metodo)
                    .Accion;

                var esAdmin = rol == nameof(RolUsuario.ADMIN);

                string mensaje;
                bool puedeSolicitar = false;

                if (accion != null && !esAdmin)
                {
                    mensaje = $"Tu perfil ({rol}) no puede {accion} directamente. " +
                              "Puedes enviar una solicitud para que un administrador lo autorice.";
                    puedeSolicitar = true;
                }
                else
                {
                    mensaje = $"Tu perfil ({rol}) no tiene permiso para realizar esta acción. " +
                              "Si la necesitas, pídesela a un administrador.";
                }

                context.Response.StatusCode  = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";

                await context.Response.WriteAsJsonAsync(new
                {
                    message = mensaje,
                    rol,
                    accion,
                    puede_solicitar = puedeSolicitar,
                });

                return;
            }

            await _predeterminado.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
