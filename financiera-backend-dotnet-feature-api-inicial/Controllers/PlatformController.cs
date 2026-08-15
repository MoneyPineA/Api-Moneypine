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

            // MONEYPINE-MT: Fase 3 — total_pagos y cartera_vencida son agregados
            // (conteo/suma), nunca filas de cartera individuales.
            var totalPagos = await _context.Pagos.CountAsync(p => p.prestamista_id == id);
            var carteraVencida = await _context.Prestamos
                .Where(p => p.prestamista_id == id && p.estatus == EstatusPrestamo.ATRASADO)
                .SumAsync(p => (decimal?)p.saldo_actual) ?? 0m;

            // MONEYPINE-MT: ultimo_acceso = ultima_actividad más reciente entre los
            // usuarios del tenant (columna ya existente, alimentada por
            // PresenceTrackingMiddleware en cada request autenticado). Se excluye
            // PLATFORM_ADMIN — no es "un usuario" de este tenant aunque su fila
            // técnica cuelgue de prestamista_id=1. List<DateTime?>.Max() ya ignora
            // nulls y devuelve null si no hay ninguno (o la lista viene vacía) —
            // no hace falta un Any() aparte.
            var actividades = await _context.Usuarios
                .Where(u => u.prestamista_id == id && u.rol != RolUsuario.PLATFORM_ADMIN)
                .Select(u => u.ultima_actividad)
                .ToListAsync();
            DateTime? ultimoAcceso = actividades.Max();

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
                trabajadores_por_rol = porRol,
                correo_contacto = prestamista.correo_contacto,
                telefono_contacto = prestamista.telefono_contacto,
                persona_contacto = prestamista.persona_contacto,
                direccion = prestamista.direccion,
                ciudad = prestamista.ciudad,
                estado = prestamista.estado,
                notas = prestamista.notas,
                total_pagos = totalPagos,
                cartera_vencida = carteraVencida,
                ultimo_acceso = ultimoAcceso
            };

            return Ok(detail);
        }

        // ────────────────────────────────────────────────────────────────
        // PUT api/platform/prestamistas/{id}
        // Edita los datos administrables de un prestamista ya dado de alta.
        // El slug NUNCA es editable aquí — es el identificador estable del
        // tenant; si viene en el body se rechaza explícitamente.
        // ────────────────────────────────────────────────────────────────
        [HttpPut("prestamistas/{id}")]
        public async Task<IActionResult> ActualizarPrestamista(int id, [FromBody] PrestamistaUpdateDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.slug))
            {
                return BadRequest(new
                {
                    message = "El slug no es editable. Es el identificador estable del tenant (URLs, config, " +
                               "integraciones futuras lo referencian); cambiarlo rompería esas referencias. " +
                               "Quita 'slug' del body para continuar."
                });
            }

            var prestamista = await _context.Prestamistas.FirstOrDefaultAsync(p => p.prestamista_id == id);
            if (prestamista == null)
                return NotFound(new { message = "Prestamista no encontrado." });

            if (string.IsNullOrWhiteSpace(dto.nombre_comercial))
                return BadRequest(new { message = "nombre_comercial es requerido." });

            if (!string.IsNullOrWhiteSpace(dto.correo_contacto) && !new EmailAddressAttribute().IsValid(dto.correo_contacto))
                return BadRequest(new { message = "correo_contacto no es un correo válido." });

            // MONEYPINE-MT: registro de auditoría con el detalle de qué cambió.
            var cambios = new List<string>();
            void Registrar(string campo, string? antes, string? despues)
            {
                if (antes != despues) cambios.Add($"{campo}: '{antes}' -> '{despues}'");
            }

            var nombreNuevo = dto.nombre_comercial.Trim();
            Registrar("nombre_comercial", prestamista.nombre_comercial, nombreNuevo);
            prestamista.nombre_comercial = nombreNuevo;

            Registrar("razon_social", prestamista.razon_social, dto.razon_social);
            prestamista.razon_social = dto.razon_social;

            Registrar("rfc", prestamista.rfc, dto.rfc);
            prestamista.rfc = dto.rfc;

            // plan/moneda/zona_horaria son NOT NULL en la entidad — si vienen
            // vacíos/omitidos se conserva el valor actual en vez de intentar
            // guardar '' o forzar un error de BD.
            if (!string.IsNullOrWhiteSpace(dto.plan))
            {
                Registrar("plan", prestamista.plan, dto.plan.Trim());
                prestamista.plan = dto.plan.Trim();
            }
            if (!string.IsNullOrWhiteSpace(dto.moneda))
            {
                Registrar("moneda", prestamista.moneda, dto.moneda.Trim());
                prestamista.moneda = dto.moneda.Trim();
            }
            if (!string.IsNullOrWhiteSpace(dto.zona_horaria))
            {
                Registrar("zona_horaria", prestamista.zona_horaria, dto.zona_horaria.Trim());
                prestamista.zona_horaria = dto.zona_horaria.Trim();
            }

            Registrar("correo_contacto", prestamista.correo_contacto, dto.correo_contacto);
            prestamista.correo_contacto = dto.correo_contacto;

            Registrar("telefono_contacto", prestamista.telefono_contacto, dto.telefono_contacto);
            prestamista.telefono_contacto = dto.telefono_contacto;

            Registrar("persona_contacto", prestamista.persona_contacto, dto.persona_contacto);
            prestamista.persona_contacto = dto.persona_contacto;

            Registrar("direccion", prestamista.direccion, dto.direccion);
            prestamista.direccion = dto.direccion;

            Registrar("ciudad", prestamista.ciudad, dto.ciudad);
            prestamista.ciudad = dto.ciudad;

            Registrar("estado", prestamista.estado, dto.estado);
            prestamista.estado = dto.estado;

            Registrar("notas", prestamista.notas, dto.notas);
            prestamista.notas = dto.notas;

            if (cambios.Count > 0)
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    prestamista_id = id,
                    Type = ActivityType.CUSTOM,
                    ClientId = 0,
                    Amount = 0,
                    Priority = NotificationLevel.NEUTRAL,
                    Description = $"PLATFORM_ADMIN actualizó datos del prestamista '{prestamista.nombre_comercial}': {string.Join("; ", cambios)}.",
                    UserId = GetCallerId(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                prestamista_id = prestamista.prestamista_id,
                slug = prestamista.slug,
                nombre_comercial = prestamista.nombre_comercial,
                razon_social = prestamista.razon_social,
                rfc = prestamista.rfc,
                plan = prestamista.plan,
                moneda = prestamista.moneda,
                zona_horaria = prestamista.zona_horaria,
                correo_contacto = prestamista.correo_contacto,
                telefono_contacto = prestamista.telefono_contacto,
                persona_contacto = prestamista.persona_contacto,
                direccion = prestamista.direccion,
                ciudad = prestamista.ciudad,
                estado = prestamista.estado,
                notas = prestamista.notas
            });
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
        // GET api/platform/prestamistas/{id}/usuarios
        // Trabajadores del tenant (excluye CLIENTE y PLATFORM_ADMIN). Nunca
        // incluye password_hash.
        // ────────────────────────────────────────────────────────────────
        [HttpGet("prestamistas/{id}/usuarios")]
        public async Task<IActionResult> GetUsuariosDePrestamista(int id)
        {
            if (!await _context.Prestamistas.AnyAsync(p => p.prestamista_id == id))
                return NotFound(new { message = "Prestamista no encontrado." });

            var usuarios = await _context.Usuarios
                .Where(u => u.prestamista_id == id && u.rol != RolUsuario.CLIENTE && u.rol != RolUsuario.PLATFORM_ADMIN)
                .OrderBy(u => u.usuario_id)
                .ToListAsync();

            return Ok(usuarios.Select(MapUsuarioDto));
        }

        // ────────────────────────────────────────────────────────────────
        // PATCH api/platform/prestamistas/{id}/usuarios/{usuarioId}
        // Edita correo/nombre/apellido/telefono/rol de un trabajador del tenant.
        // Patch parcial: solo toca los campos no-nulos del body.
        // ────────────────────────────────────────────────────────────────
        [HttpPatch("prestamistas/{id}/usuarios/{usuarioId}")]
        public async Task<IActionResult> ActualizarUsuarioDePrestamista(int id, int usuarioId, [FromBody] PrestamistaUsuarioUpdateDto dto)
        {
            var (usuario, error) = await ResolverUsuarioDelTenant(id, usuarioId);
            if (error != null) return error;

            // Regla de seguridad: PLATFORM_ADMIN no puede modificarse a sí mismo
            // por estos endpoints (rol, correo, ni nada) — la cuenta con la que
            // opera este panel no es "un trabajador" de ningún tenant.
            if (usuario!.usuario_id == GetCallerId())
                return BadRequest(new { message = "No puedes modificarte a ti mismo desde este panel." });

            if (dto.rol.HasValue && dto.rol.Value == RolUsuario.PLATFORM_ADMIN)
            {
                return BadRequest(new
                {
                    message = "No se puede asignar el rol PLATFORM_ADMIN desde aquí: crearía un administrador " +
                               "de plataforma dentro de un tenant."
                });
            }

            var cambios = new List<string>();

            if (dto.correo != null)
            {
                var correoNuevo = dto.correo.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(correoNuevo) || !new EmailAddressAttribute().IsValid(correoNuevo))
                    return BadRequest(new { message = "correo no es un correo válido." });

                if (correoNuevo != usuario.correo)
                {
                    // MONEYPINE-MT: correo único GLOBAL (no solo dentro del tenant) —
                    // mismo criterio que el alta de prestamista/usuario en este mismo
                    // controller y en UsuarioController.
                    if (await _context.Usuarios.AnyAsync(u => u.correo == correoNuevo && u.usuario_id != usuarioId))
                        return Conflict(new { message = "Ya existe otro usuario con ese correo." });

                    cambios.Add($"correo: '{usuario.correo}' -> '{correoNuevo}'");
                    usuario.correo = correoNuevo;
                }
            }

            if (dto.nombre != null)
            {
                var nombreNuevo = dto.nombre.Trim();
                if (string.IsNullOrWhiteSpace(nombreNuevo))
                    return BadRequest(new { message = "nombre no puede quedar vacío." });
                if (nombreNuevo != usuario.nombre)
                {
                    cambios.Add($"nombre: '{usuario.nombre}' -> '{nombreNuevo}'");
                    usuario.nombre = nombreNuevo;
                }
            }

            if (dto.apellido != null)
            {
                var apellidoNuevo = dto.apellido.Trim();
                if (string.IsNullOrWhiteSpace(apellidoNuevo))
                    return BadRequest(new { message = "apellido no puede quedar vacío." });
                if (apellidoNuevo != usuario.apellido)
                {
                    cambios.Add($"apellido: '{usuario.apellido}' -> '{apellidoNuevo}'");
                    usuario.apellido = apellidoNuevo;
                }
            }

            if (dto.telefono != null && dto.telefono != usuario.telefono)
            {
                cambios.Add($"telefono: '{usuario.telefono}' -> '{dto.telefono}'");
                usuario.telefono = dto.telefono;
            }

            if (dto.rol.HasValue && dto.rol.Value != usuario.rol)
            {
                // Regla de seguridad: no dejar al tenant sin ningún ADMIN activo.
                if (usuario.rol == RolUsuario.ADMIN && usuario.estado == EstadoUsuario.ACTIVO)
                {
                    if (await QuedaSinAdminActivo(id, usuarioId))
                    {
                        return BadRequest(new
                        {
                            message = "No puedes cambiar el rol del último ADMIN activo de este prestamista: " +
                                       "lo dejaría sin nadie que administre su propio sistema."
                        });
                    }
                }

                cambios.Add($"rol: '{usuario.rol}' -> '{dto.rol.Value}'");
                usuario.rol = dto.rol.Value;
            }

            if (cambios.Count == 0)
                return Ok(MapUsuarioDto(usuario));

            _context.ActivityLogs.Add(new ActivityLog
            {
                prestamista_id = id,
                Type = ActivityType.CUSTOM,
                ClientId = 0,
                Amount = 0,
                Priority = NotificationLevel.NEUTRAL,
                Description = $"PLATFORM_ADMIN modificó al usuario '{usuario.correo}' (id {usuario.usuario_id}): {string.Join("; ", cambios)}.",
                UserId = GetCallerId(),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(MapUsuarioDto(usuario));
        }

        // ────────────────────────────────────────────────────────────────
        // POST api/platform/prestamistas/{id}/usuarios/{usuarioId}/reset-password
        // Genera una contraseña temporal, la hashea y la devuelve UNA sola vez.
        // No se puede recuperar después: solo se guarda el hash.
        // ────────────────────────────────────────────────────────────────
        [HttpPost("prestamistas/{id}/usuarios/{usuarioId}/reset-password")]
        public async Task<IActionResult> ResetPasswordUsuarioDePrestamista(int id, int usuarioId)
        {
            var (usuario, error) = await ResolverUsuarioDelTenant(id, usuarioId);
            if (error != null) return error;

            if (usuario!.usuario_id == GetCallerId())
                return BadRequest(new { message = "No puedes restablecer tu propia contraseña desde este panel." });

            var passwordTemporal = GenerarPasswordTemporal();
            var hasher = new PasswordHasher<Usuario>();
            usuario.password_hash = hasher.HashPassword(usuario, passwordTemporal);

            _context.ActivityLogs.Add(new ActivityLog
            {
                prestamista_id = id,
                Type = ActivityType.CUSTOM,
                ClientId = 0,
                Amount = 0,
                Priority = NotificationLevel.HIGH,
                Description = $"PLATFORM_ADMIN restableció la contraseña del usuario '{usuario.correo}' (id {usuario.usuario_id}).",
                UserId = GetCallerId(),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new ResetPasswordResponseDto
            {
                usuario_id = usuario.usuario_id,
                correo = usuario.correo,
                password_temporal = passwordTemporal
            });
        }

        // ────────────────────────────────────────────────────────────────
        // PATCH api/platform/prestamistas/{id}/usuarios/{usuarioId}/estado
        // ACTIVO / INACTIVO / BLOQUEADO.
        // ────────────────────────────────────────────────────────────────
        [HttpPatch("prestamistas/{id}/usuarios/{usuarioId}/estado")]
        public async Task<IActionResult> ActualizarEstadoUsuarioDePrestamista(int id, int usuarioId, [FromBody] PrestamistaUsuarioEstadoUpdateDto dto)
        {
            var (usuario, error) = await ResolverUsuarioDelTenant(id, usuarioId);
            if (error != null) return error;

            if (usuario!.usuario_id == GetCallerId())
                return BadRequest(new { message = "No puedes cambiar el estado de tu propia cuenta desde este panel." });

            if (dto.estado == usuario.estado)
                return Ok(MapUsuarioDto(usuario));

            // Regla de seguridad: no dejar al tenant sin ningún ADMIN activo.
            if (usuario.rol == RolUsuario.ADMIN && usuario.estado == EstadoUsuario.ACTIVO && dto.estado != EstadoUsuario.ACTIVO)
            {
                if (await QuedaSinAdminActivo(id, usuarioId))
                {
                    return BadRequest(new
                    {
                        message = "No puedes desactivar/bloquear al último ADMIN activo de este prestamista: " +
                                   "lo dejaría sin nadie que administre su propio sistema."
                    });
                }
            }

            var estadoAnterior = usuario.estado;
            usuario.estado = dto.estado;

            _context.ActivityLogs.Add(new ActivityLog
            {
                prestamista_id = id,
                Type = ActivityType.CUSTOM,
                ClientId = 0,
                Amount = 0,
                Priority = dto.estado == EstadoUsuario.ACTIVO ? NotificationLevel.NEUTRAL : NotificationLevel.HIGH,
                Description = $"PLATFORM_ADMIN cambió el estado del usuario '{usuario.correo}' (id {usuario.usuario_id}) de {estadoAnterior} a {dto.estado}.",
                UserId = GetCallerId(),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(MapUsuarioDto(usuario));
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

        // Nunca incluye password_hash.
        private static PrestamistaUsuarioDto MapUsuarioDto(Usuario u) => new()
        {
            usuario_id = u.usuario_id,
            correo = u.correo,
            nombre = u.nombre,
            apellido = u.apellido,
            telefono = u.telefono,
            rol = u.rol.ToString(),
            estado = u.estado.ToString(),
            fecha_registro = u.fecha_registro,
            ultima_actividad = u.ultima_actividad
        };

        // MONEYPINE-MT: DoD del contrato — un usuarioId que exista pero sea de
        // OTRO tenant responde 404, nunca 403 (no revelar que ese id existe en
        // otro tenant). Las cuentas PLATFORM_ADMIN tampoco se administran por
        // estos endpoints (400): no son "un trabajador" de ningún tenant, aunque
        // su fila técnica cuelgue de un prestamista_id concreto. Esto además
        // cubre por completo la regla "PLATFORM_ADMIN no puede modificarse a sí
        // mismo" para el único caso posible hoy (la propia cuenta que llama);
        // el chequeo explícito de auto-modificación en cada endpoint es una
        // segunda capa por si en el futuro existiera más de una cuenta PLATFORM_ADMIN.
        private async Task<(Usuario? usuario, IActionResult? error)> ResolverUsuarioDelTenant(int prestamistaId, int usuarioId)
        {
            if (!await _context.Prestamistas.AnyAsync(p => p.prestamista_id == prestamistaId))
                return (null, NotFound(new { message = "Prestamista no encontrado." }));

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.usuario_id == usuarioId);
            if (usuario == null || usuario.prestamista_id != prestamistaId)
                return (null, NotFound(new { message = "Usuario no encontrado." }));

            if (usuario.rol == RolUsuario.PLATFORM_ADMIN)
                return (null, BadRequest(new { message = "Las cuentas PLATFORM_ADMIN no se administran desde el panel de prestamistas." }));

            return (usuario, null);
        }

        // true si, tras excluir a usuarioId, no queda ningún ADMIN ACTIVO en el tenant.
        private async Task<bool> QuedaSinAdminActivo(int prestamistaId, int usuarioId) =>
            !await _context.Usuarios.AnyAsync(u =>
                u.prestamista_id == prestamistaId &&
                u.rol == RolUsuario.ADMIN &&
                u.estado == EstadoUsuario.ACTIVO &&
                u.usuario_id != usuarioId);

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
