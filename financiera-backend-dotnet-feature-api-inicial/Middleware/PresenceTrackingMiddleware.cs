using System.Security.Claims;
using ApiEjemplo.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Middleware
{
    // MONEYPINE-FIX: registra ultima_actividad/ultimo_user_agent del usuario autenticado en
    // cada request. Es la base de "Reportes/Trabajadores conectados": el estado (En línea /
    // Ausente / Desconectado) se deriva de este timestamp, sin necesitar WebSockets.
    public class PresenceTrackingMiddleware
    {
        private readonly RequestDelegate _next;

        public PresenceTrackingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(idClaim, out var usuarioId))
                    {
                        var userAgent = context.Request.Headers.UserAgent.ToString();
                        if (userAgent.Length > 255) userAgent = userAgent[..255];

                        var db = context.RequestServices.GetRequiredService<AppDbContext>();
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE usuario SET ultima_actividad = {DateTime.UtcNow}, ultimo_user_agent = {userAgent} WHERE usuario_id = {usuarioId}");
                    }
                }
                catch
                {
                    // No bloquear la request si falla el registro de presencia
                }
            }

            await _next(context);
        }
    }
}
