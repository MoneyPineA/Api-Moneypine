namespace ApiEjemplo.Tenancy
{
    // MONEYPINE-MT: resuelve el tenant de la request desde el claim "prestamista_id"
    // del JWT (Fase 1 — Parte 4.5). Debe registrarse DESPUÉS de UseAuthentication()
    // y ANTES de UseAuthorization(): necesita context.User ya poblado, y el resto
    // del pipeline (controllers) necesita ITenantContext ya resuelto.
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                // MONEYPINE-MT: PLATFORM_ADMIN no existe todavía como rol (llega en
                // Fase 2 — seguridad-auth). IsInRole simplemente no matchea hoy, así
                // que esPlataforma queda en false para todos los usuarios actuales.
                var esPlataforma = context.User.IsInRole("PLATFORM_ADMIN");
                var claim = context.User.FindFirst("prestamista_id")?.Value;

                if (!esPlataforma && !int.TryParse(claim, out _))
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "Token sin tenant asignado"
                    });
                    return;
                }

                tenant.Establecer(int.TryParse(claim, out var id) ? id : 0, esPlataforma);
            }

            await _next(context);
        }
    }
}
