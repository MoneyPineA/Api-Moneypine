using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ApiEjemplo.Data;
using ApiEjemplo.DTOs.Platform;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Controllers
{
    // MONEYPINE-MT: Fase 3 — panel de plataforma. Rol PLATFORM_ADMIN, "Alta/baja
    // de prestamistas. Nunca ve cartera" (documento de arquitectura). La barrera
    // que impide que este rol pise cualquier otra ruta vive en
    // Tenancy/PlatformScopeMiddleware.cs — este controller solo expone agregados.
    //
    // Nota sobre I4: las consultas de aquí NO usan IgnoreQueryFilters(). No hace
    // falta — EsPlataforma es true para PLATFORM_ADMIN, y el query filter global
    // (`EsPlataforma || prestamista_id == TenantId`) ya deja pasar todas las filas
    // de todos los tenants cuando EsPlataforma es true. Es la única superficie del
    // sistema donde eso es intencional.
    [ApiController]
    [Route("api/platform")]
    [Authorize(Roles = "PLATFORM_ADMIN")]
    public class PlatformController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PlatformController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCallerId() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        // ────────────────────────────────────────────────────────────────
        // GET api/platform/prestamistas
        // Lista con métricas agregadas por tenant. Solo agregados, jamás
        // filas de cartera.
        // ────────────────────────────────────────────────────────────────
        [HttpGet("prestamistas")]
        public async Task<IActionResult> GetPrestamistas()
        {
            var prestamistas = await _context.Prestamistas
                .OrderBy(p => p.prestamista_id)
                .ToListAsync();

            var (trabajadores, clientes, creditos) = await CargarAgregados();

            var resultado = prestamistas.Select(p => MapMetrics(p, trabajadores, clientes, creditos)).ToList();
            return Ok(resultado);
        }

        // ────────────────────────────────────────────────────────────────
        // GET api/platform/prestamistas/{id}
        // Mismo detalle de uno, más desglose de trabajadores por rol.
        // ────────────────────────────────────────────────────────────────
        [HttpGet("prestamistas/{id}")]
        public async Task<IActionResult> GetPrestamista(int id)
        {
            var prestamista = await _context.Prestamistas.FirstOrDefaultAsync(p => p.prestamista_id == id);
            if (prestamista == null)
                return NotFound(new { message = "Prestamista no encontrado." });

            var (trabajadores, clientes, creditos) = await CargarAgregados();
            var metrics = MapMetrics(prestamista, trabajadores, clientes, creditos);

            var porRol = await _context.Usuarios
                .Where(u => u.prestamista_id == id && u.rol != RolUsuario.CLIENTE)
                .GroupBy(u => u.rol)
                .Select(g => new TrabajadoresPorRolDto { rol = g.Key.ToString(), total = g.Count() })
                .ToListAsync();

            var detail = new PrestamistaDetailDto
            {
                prestamista_id = metrics.prestamista_id,
                slug = metrics.slug,
                nombre_comercial = metrics.nombre_comercial,
                estatus = metrics.estatus,
                plan = metrics.plan,
                fecha_alta = metrics.fecha_alta,
                total_trabajadores = metrics.total_trabajadores,
                total_clientes = metrics.total_clientes,
                total_creditos = metrics.total_creditos,
                creditos_activos = metrics.creditos_activos,
                cartera_total = metrics.cartera_total,
                razon_social = prestamista.razon_social,
                rfc = prestamista.rfc,
                moneda = prestamista.moneda,
                zona_horaria = prestamista.zona_horaria,
                trabajadores_por_rol = porRol
            };

            return Ok(detail);
        }

        // ────────────────────────────────────────────────────────────────
        // GET api/platform/resumen
        // Totales de la plataforma.
        // ────────────────────────────────────────────────────────────────
        [HttpGet("resumen")]
        public async Task<IActionResult> GetResumen()
        {
            var resumen = new ResumenPlataformaDto
            {
                prestamistas_total = await _context.Prestamistas.CountAsync(),
                prestamistas_activos = await _context.Prestamistas.CountAsync(p => p.estatus == EstatusPrestamista.ACTIVO),
                prestamistas_suspendidos = await _context.Prestamistas.CountAsync(p => p.estatus == EstatusPrestamista.SUSPENDIDO),
                prestamistas_cancelados = await _context.Prestamistas.CountAsync(p => p.estatus == EstatusPrestamista.CANCELADO),
                total_trabajadores = await _context.Usuarios.CountAsync(u => u.rol != RolUsuario.CLIENTE),
                total_clientes = await _context.Clientes.CountAsync(),
                total_creditos = await _context.Prestamos.CountAsync(),
                creditos_activos = await _context.Prestamos.CountAsync(p =>
                    p.estatus == EstatusPrestamo.ACTIVO || p.estatus == EstatusPrestamo.ATRASADO),
                cartera_total = await _context.Prestamos
                    .Where(p => p.estatus != EstatusPrestamo.LIQUIDADO)
                    .SumAsync(p => (decimal?)p.saldo_actual) ?? 0m
            };

            return Ok(resumen);
        }

        // ────────────────────────────────────────────────────────────────
        // POST api/platform/prestamistas
        // Alta en una transacción: prestamista + ADMIN inicial + gerencia
        // principal + productos de crédito básicos + conceptos del sistema
        // (copiados del tenant 1) + registro en ActivityLogs.
        // ────────────────────────────────────────────────────────────────
        [HttpPost("prestamistas")]
        public async Task<IActionResult> CrearPrestamista([FromBody] PrestamistaCreateDto dto)
        {
            var slug = dto.slug?.Trim().ToLowerInvariant() ?? "";
            if (!Regex.IsMatch(slug, "^[a-z0-9]+(-[a-z0-9]+)*$"))
                return BadRequest(new { message = "El slug debe ir en minúsculas, sin espacios ni acentos (letras, números y guiones)." });

            if (string.IsNullOrWhiteSpace(dto.nombre_comercial))
                return BadRequest(new { message = "nombre_comercial es requerido." });

            if (string.IsNullOrWhiteSpace(dto.admin_correo) || !new EmailAddressAttribute().IsValid(dto.admin_correo))
                return BadRequest(new { message = "admin_correo no es un correo válido." });

            if (string.IsNullOrWhiteSpace(dto.admin_nombre) || string.IsNullOrWhiteSpace(dto.admin_apellido))
                return BadRequest(new { message = "admin_nombre y admin_apellido son requeridos." });

            if (await _context.Prestamistas.AnyAsync(p => p.slug == slug))
                return Conflict(new { message = $"Ya existe un prestamista con el slug '{slug}'." });

            var correoAdmin = dto.admin_correo.Trim().ToLowerInvariant();
            if (await _context.Usuarios.AnyAsync(u => u.correo == correoAdmin))
                return Conflict(new { message = "Ya existe un usuario con ese correo." });

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahora = DateTime.UtcNow;

                // 1. INSERT prestamista
                var prestamista = new Prestamista
                {
                    slug = slug,
                    nombre_comercial = dto.nombre_comercial.Trim(),
                    razon_social = dto.razon_social,
                    rfc = dto.rfc,
                    estatus = EstatusPrestamista.ACTIVO,
                    plan = string.IsNullOrWhiteSpace(dto.plan) ? "MVP" : dto.plan!.Trim(),
                    moneda = string.IsNullOrWhiteSpace(dto.moneda) ? "MXN" : dto.moneda!.Trim(),
                    zona_horaria = string.IsNullOrWhiteSpace(dto.zona_horaria) ? "America/Mexico_City" : dto.zona_horaria!.Trim(),
                    fecha_alta = ahora
                };
                _context.Prestamistas.Add(prestamista);
                // MONEYPINE-MT: se guarda ya para obtener el prestamista_id
                // autogenerado antes de crear las entidades hijas (todas lo
                // necesitan como FK explícita — ver nota más abajo).
                await _context.SaveChangesAsync();

                // 2. Usuario ADMIN inicial con contraseña temporal
                var passwordTemporal = GenerarPasswordTemporal();
                var admin = new Usuario
                {
                    // MONEYPINE-MT: EsPlataforma es true en este request (llamador
                    // PLATFORM_ADMIN), así que el guard de SaveChanges en
                    // AppDbContext NO fuerza el tenant automáticamente (esa rama
                    // solo aplica cuando !EsPlataforma). Hay que asignar
                    // prestamista_id a mano en cada entidad o queda en 0.
                    prestamista_id = prestamista.prestamista_id,
                    correo = correoAdmin,
                    nombre = dto.admin_nombre.Trim(),
                    apellido = dto.admin_apellido.Trim(),
                    telefono = dto.admin_telefono,
                    rol = RolUsuario.ADMIN,
                    estado = EstadoUsuario.ACTIVO,
                    fecha_registro = ahora
                };
                // MONEYPINE-MT: PasswordHasher de ASP.NET Identity — es el mismo
                // usado por UsuarioController.CrearUsuario para altas nuevas
                // (BCrypt solo convive para hashes heredados, ver AuthController.Login).
                var hasher = new PasswordHasher<Usuario>();
                admin.password_hash = hasher.HashPassword(admin, passwordTemporal);
                _context.Usuarios.Add(admin);

                // 3. Seed de una gerencia principal
                _context.Gerencias.Add(new Gerencia
                {
                    prestamista_id = prestamista.prestamista_id,
                    nombre = "MATRIZ",
                    codigo = "01",
                    es_principal = true
                });

                // 4. Seed de producto_credito básicos (semanal, quincenal, mensual)
                _context.ProductosCredito.AddRange(
                    new ProductoCredito
                    {
                        prestamista_id = prestamista.prestamista_id,
                        tipo_credito = "PERSONAL",
                        nombre = "Semanal",
                        forma_pago = "SEMANAL",
                        plazo = 16,
                        tasa_interes = 10,
                        es_defecto = true,
                        activo = true
                    },
                    new ProductoCredito
                    {
                        prestamista_id = prestamista.prestamista_id,
                        tipo_credito = "PERSONAL",
                        nombre = "Quincenal",
                        forma_pago = "QUINCENAL",
                        plazo = 8,
                        tasa_interes = 10,
                        activo = true
                    },
                    new ProductoCredito
                    {
                        prestamista_id = prestamista.prestamista_id,
                        tipo_credito = "PERSONAL",
                        nombre = "Mensual",
                        forma_pago = "MENSUAL",
                        plazo = 4,
                        tasa_interes = 10,
                        activo = true
                    }
                );

                // 5. Seed de concepto_sistema — copiando los del tenant 1
                var conceptosBase = await _context.ConceptosSistema
                    .Where(c => c.prestamista_id == 1)
                    .OrderBy(c => c.orden)
                    .ToListAsync();
                foreach (var c in conceptosBase)
                {
                    _context.ConceptosSistema.Add(new ConceptoSistema
                    {
                        prestamista_id = prestamista.prestamista_id,
                        nombre = c.nombre,
                        tipo = c.tipo,
                        activo = c.activo,
                        orden = c.orden
                    });
                }

                // 6. Registro en ActivityLogs
                _context.ActivityLogs.Add(new ActivityLog
                {
                    prestamista_id = prestamista.prestamista_id,
                    Type = ActivityType.CUSTOM,
                    ClientId = 0,
                    Amount = 0,
                    Priority = NotificationLevel.NEUTRAL,
                    Description = $"Alta de prestamista '{prestamista.nombre_comercial}' (slug '{prestamista.slug}') por PLATFORM_ADMIN.",
                    UserId = GetCallerId(),
                    CreatedAt = ahora
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return CreatedAtAction(nameof(GetPrestamista), new { id = prestamista.prestamista_id }, new PrestamistaCreateResponseDto
                {
                    prestamista_id = prestamista.prestamista_id,
                    slug = prestamista.slug,
                    nombre_comercial = prestamista.nombre_comercial,
                    estatus = prestamista.estatus.ToString(),
                    fecha_alta = prestamista.fecha_alta,
                    admin_usuario_id = admin.usuario_id,
                    admin_correo = admin.correo,
                    admin_password_temporal = passwordTemporal
                });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // PATCH api/platform/prestamistas/{id}/estatus
        // ACTIVO / SUSPENDIDO / CANCELADO. Suspender/cancelar el tenant 1
        // exige confirmar_tenant_1=true en el body (es el tenant de
        // producción — dejaría a todos fuera).
        // ────────────────────────────────────────────────────────────────
        [HttpPatch("prestamistas/{id}/estatus")]
        public async Task<IActionResult> ActualizarEstatus(int id, [FromBody] PrestamistaEstatusUpdateDto dto)
        {
            if (!Enum.TryParse<EstatusPrestamista>(dto.estatus, ignoreCase: true, out var nuevoEstatus))
                return BadRequest(new { message = "estatus debe ser ACTIVO, SUSPENDIDO o CANCELADO." });

            var prestamista = await _context.Prestamistas.FirstOrDefaultAsync(p => p.prestamista_id == id);
            if (prestamista == null)
                return NotFound(new { message = "Prestamista no encontrado." });

            if (id == 1 && nuevoEstatus != EstatusPrestamista.ACTIVO && !dto.confirmar_tenant_1)
            {
                return BadRequest(new
                {
                    message = "El prestamista 1 es el tenant de producción. Repite la llamada con " +
                               "\"confirmar_tenant_1\": true si de verdad quieres suspenderlo o cancelarlo."
                });
            }

            prestamista.estatus = nuevoEstatus;
            if (nuevoEstatus == EstatusPrestamista.CANCELADO)
                prestamista.fecha_baja = DateTime.UtcNow;
            else
                prestamista.fecha_baja = null;

            await _context.SaveChangesAsync();

            return Ok(new { prestamista_id = prestamista.prestamista_id, estatus = prestamista.estatus.ToString() });
        }

        // ────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────
        private async Task<(Dictionary<int, int> trabajadores, Dictionary<int, int> clientes, Dictionary<int, (int total, int activos, decimal cartera)> creditos)>
            CargarAgregados()
        {
            var trabajadores = await _context.Usuarios
                .Where(u => u.rol != RolUsuario.CLIENTE)
                .GroupBy(u => u.prestamista_id)
                .Select(g => new { PrestamistaId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.PrestamistaId, x => x.Total);

            var clientes = await _context.Clientes
                .GroupBy(c => c.prestamista_id)
                .Select(g => new { PrestamistaId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.PrestamistaId, x => x.Total);

            var creditosRaw = await _context.Prestamos
                .GroupBy(p => p.prestamista_id)
                .Select(g => new
                {
                    PrestamistaId = g.Key,
                    Total = g.Count(),
                    Activos = g.Count(p => p.estatus == EstatusPrestamo.ACTIVO || p.estatus == EstatusPrestamo.ATRASADO),
                    // MONEYPINE-MT: "no liquidados" tal cual lo pide el contrato del
                    // panel de plataforma — no se excluye CANCELADO aparte.
                    Cartera = g.Where(p => p.estatus != EstatusPrestamo.LIQUIDADO).Sum(p => (decimal?)p.saldo_actual) ?? 0m
                })
                .ToListAsync();
            var creditos = creditosRaw.ToDictionary(x => x.PrestamistaId, x => (x.Total, x.Activos, x.Cartera));

            return (trabajadores, clientes, creditos);
        }

        private static PrestamistaMetricsDto MapMetrics(
            Prestamista p,
            Dictionary<int, int> trabajadores,
            Dictionary<int, int> clientes,
            Dictionary<int, (int total, int activos, decimal cartera)> creditos)
        {
            creditos.TryGetValue(p.prestamista_id, out var c);
            return new PrestamistaMetricsDto
            {
                prestamista_id = p.prestamista_id,
                slug = p.slug,
                nombre_comercial = p.nombre_comercial,
                estatus = p.estatus.ToString(),
                plan = p.plan,
                fecha_alta = p.fecha_alta,
                total_trabajadores = trabajadores.GetValueOrDefault(p.prestamista_id),
                total_clientes = clientes.GetValueOrDefault(p.prestamista_id),
                total_creditos = c.total,
                creditos_activos = c.activos,
                cartera_total = c.cartera
            };
        }

        // MONEYPINE-MT: 16 caracteres, alfanumérico + símbolo — suficiente entropía
        // para una contraseña temporal de un solo uso que se muestra una sola vez
        // y se espera que el ADMIN nuevo cambie en su primer login.
        private static string GenerarPasswordTemporal()
        {
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
            var bytes = new byte[16];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var chars = new char[16];
            for (var i = 0; i < bytes.Length; i++)
                chars[i] = alfabeto[bytes[i] % alfabeto.Length];
            return new string(chars);
        }
    }
}
