using ApiEjemplo.Enums;

namespace ApiEjemplo.Tenancy
{
    // MONEYPINE-MT: Fase 3 — confina PLATFORM_ADMIN a /api/platform/* y /api/auth/*.
    //
    // El query filter global es `EsPlataforma || e.prestamista_id == TenantId`
    // (ver AppDbContext.AplicarFiltroTenant). Cuando EsPlataforma es true —true
    // para todo PLATFORM_ADMIN, ver TenantResolutionMiddleware— el filtro deja
    // pasar TODA la cartera de TODOS los prestamistas: es la condición correcta
    // para que PlatformController pueda agregar métricas cross-tenant, pero
    // también significa que, sin esta barrera, un PLATFORM_ADMIN que llame
    // GET /api/Cliente vería los clientes de todos los prestamistas. El
    // documento de arquitectura lo prohíbe explícitamente ("PLATFORM_ADMIN —
    // Nunca ve cartera").
    //
    // Se registra DESPUÉS de TenantResolutionMiddleware (necesita
    // context.User ya poblado) y ANTES de UseAuthorization: corta la request
    // antes de que llegue a cualquier controller/DbContext, no solo antes de
    // que un [Authorize(Roles=...)] la evalúe.
    public class PlatformScopeMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly string[] RutasPermitidas = { "/api/platform", "/api/auth" };

        public PlatformScopeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true &&
                context.User.IsInRole(nameof(RolUsuario.PLATFORM_ADMIN)))
            {
                var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
                var permitido = RutasPermitidas.Any(r => path.StartsWith(r));

                if (!permitido)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "Tu perfil (PLATFORM_ADMIN) solo puede operar el panel de plataforma (/api/platform)."
                    });
                    return;
                }
            }

            await _next(context);
        }
    }
}
