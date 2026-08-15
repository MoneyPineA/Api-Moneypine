using ApiEjemplo.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Tenancy
{
    // MONEYPINE-MT: valida que el subdominio de la peticion (puebla.moneypine.com.mx)
    // coincida con el tenant que trae el JWT.
    //
    // ==> EL HOST NUNCA DECIDE EL TENANT. <==
    //
    // Esto es deliberado y es el punto entero de esta clase. La cabecera Host la
    // controla quien hace la peticion: si el tenant saliera de ahi, bastaria con
    // cambiar la URL (o mandar un Host a mano con curl) para saltar de financiera.
    // El tenant sale del claim firmado en el token — lo resuelve
    // TenantResolutionMiddleware, que corre ANTES que este — y aqui solo se
    // comprueba que el subdominio no lo contradiga.
    //
    // En un desacuerdo se responde 404, no 403: un 403 confirmaria que ese
    // subdominio existe y a quien pertenece. El 404 no distingue entre "no es tuyo"
    // y "no existe", que es justo lo que queremos que vea un extrano.
    public class HostTenantGuardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HostTenantGuardMiddleware> _logger;

        // Subdominios que no son de ningun prestamista. "api" y "www" son de
        // infraestructura; el apex (moneypine.com.mx, sin subdominio) tampoco lo es.
        private static readonly HashSet<string> Reservados = new(StringComparer.OrdinalIgnoreCase)
        {
            "www", "api", "admin", "app", "panel", "plataforma", "staging", "localhost",
        };

        public HostTenantGuardMiddleware(RequestDelegate next, ILogger<HostTenantGuardMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenant, AppDbContext db)
        {
            var slug = ExtraerSlug(context.Request.Host.Host);

            // Sin subdominio util (apex, api.*, localhost, IP de Railway) no hay nada
            // que contrastar: manda el token, que es como funciona hoy.
            if (slug is null || context.User?.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            // El admin de plataforma no pertenece a ninguna financiera; PlatformScopeMiddleware
            // ya lo confina a /api/platform/*. No se le exige que el host cuadre.
            if (tenant.EsPlataforma)
            {
                await _next(context);
                return;
            }

            // IgnoreQueryFilters es obligatorio aqui: prestamista NO es una entidad de
            // tenant filtrada, pero se consulta antes de confiar en nada, y hay que poder
            // ver la fila de CUALQUIER slug para compararla. Es una lectura de un unico
            // campo (el id) de la tabla de tenants, no de datos de negocio.
            var idDelHost = await db.Prestamistas
                .IgnoreQueryFilters()
                .Where(p => p.slug == slug)
                .Select(p => (int?)p.prestamista_id)
                .FirstOrDefaultAsync();

            // Subdominio que no corresponde a ningun prestamista: 404 sin filtrar si
            // el slug existe o no.
            if (idDelHost is null || idDelHost.Value != tenant.PrestamistaId)
            {
                _logger.LogWarning(
                    "Host/tenant no coinciden: host '{Host}' (slug '{Slug}') con token del tenant {TenantId} en {Ruta}",
                    context.Request.Host.Host, slug, tenant.PrestamistaId, context.Request.Path);

                context.Response.StatusCode = 404;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "No encontrado",
                    message = "El recurso solicitado no existe.",
                });
                return;
            }

            await _next(context);
        }

        // "puebla.moneypine.com.mx" -> "puebla";  "moneypine.com.mx" -> null
        internal static string? ExtraerSlug(string? host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;

            // Host.Host ya viene sin puerto, pero se recorta por si acaso.
            var limpio = host.Split(':')[0].Trim().TrimEnd('.');
            if (limpio.Length == 0) return null;

            // Una IP no lleva subdominio (Railway resuelve por IP en healthchecks).
            if (System.Net.IPAddress.TryParse(limpio, out _)) return null;

            var partes = limpio.Split('.');
            // Hace falta subdominio + dominio + TLD como minimo ("a.b.c").
            // "moneypine.com.mx" son 3 partes pero el apex de un .com.mx tambien,
            // asi que se exige que sobre algo por delante del dominio conocido.
            if (partes.Length < 3) return null;

            var candidato = partes[0];
            if (Reservados.Contains(candidato)) return null;

            // El apex de un dominio de segundo nivel ("moneypine.com.mx") tiene 3
            // partes y su primera parte es el dominio, no un subdominio. Se descarta
            // comparando contra los sufijos compuestos habituales en MX.
            var resto = string.Join('.', partes.Skip(1));
            if (resto is "com.mx" or "org.mx" or "net.mx" or "gob.mx") return null;

            return candidato.ToLowerInvariant();
        }
    }
}
