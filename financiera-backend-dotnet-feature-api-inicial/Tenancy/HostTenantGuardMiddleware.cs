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
        private readonly string _dominioTenants;

        // MONEYPINE-FIX: dominio bajo el cual cuelgan los subdominios de tenant.
        // Sobrescribible con "Tenancy:DominioTenants" en appsettings para que
        // staging o un dominio nuevo no obliguen a recompilar.
        private const string DominioPorDefecto = "moneypine.com.mx";

        // Subdominios que no son de ningun prestamista. "api" y "www" son de
        // infraestructura; el apex (moneypine.com.mx, sin subdominio) tampoco lo es.
        private static readonly HashSet<string> Reservados = new(StringComparer.OrdinalIgnoreCase)
        {
            "www", "api", "admin", "app", "panel", "plataforma", "staging", "localhost",
        };

        public HostTenantGuardMiddleware(
            RequestDelegate next,
            ILogger<HostTenantGuardMiddleware> logger,
            IConfiguration config)
        {
            _next = next;
            _logger = logger;
            _dominioTenants = config["Tenancy:DominioTenants"] ?? DominioPorDefecto;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenant, AppDbContext db)
        {
            var slug = ExtraerSlug(context.Request.Host.Host, _dominioTenants);

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
        //
        // MONEYPINE-FIX: se pasa de LISTA NEGRA a LISTA BLANCA. La version anterior
        // trataba como slug de tenant el primer segmento de CUALQUIER host con 3+
        // partes, y solo descartaba los que reconocia (Reservados + sufijos .mx).
        // El host real del backend en Railway —api-moneypine-production.up.railway.app—
        // no caia en ninguno de los dos filtros: "api-moneypine-production" no es
        // igual a "api" (el HashSet compara por igualdad exacta, no por prefijo) y
        // "up.railway.app" no esta en la lista de sufijos. Resultado: se extraia
        // "api-moneypine-production" como slug, ningun prestamista lo tenia, y el
        // guard respondia 404 a TODA peticion autenticada, de TODOS los tenants.
        // Login seguia funcionando (es anonimo y sale antes), asi que el sintoma era
        // una app que entraba bien y luego mostraba todo vacio.
        //
        // Con lista blanca, cualquier host que no cuelgue del dominio de tenants
        // devuelve null y manda el token, que es el comportamiento correcto: el host
        // solo puede CONTRADECIR al token, nunca decidir por su cuenta.
        internal static string? ExtraerSlug(string? host, string dominioTenants)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            if (string.IsNullOrWhiteSpace(dominioTenants)) return null;

            // Host.Host ya viene sin puerto, pero se recorta por si acaso.
            var limpio = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();
            if (limpio.Length == 0) return null;

            // Una IP no lleva subdominio (Railway resuelve por IP en healthchecks).
            if (System.Net.IPAddress.TryParse(limpio, out _)) return null;

            var dominio = dominioTenants.Trim().TrimEnd('.').ToLowerInvariant();

            // El apex ("moneypine.com.mx") no termina en ".moneypine.com.mx", asi que
            // cae aqui y devuelve null — correcto, el apex no es de ningun tenant.
            if (!limpio.EndsWith("." + dominio, StringComparison.Ordinal)) return null;

            var candidato = limpio[..^(dominio.Length + 1)];
            if (candidato.Length == 0) return null;

            // Un solo nivel de subdominio: "puebla" si, "algo.puebla" no.
            if (candidato.Contains('.')) return null;

            if (Reservados.Contains(candidato)) return null;

            return candidato;
        }
    }
}
