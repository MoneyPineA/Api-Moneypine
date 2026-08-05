using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.DTOs.Grupo;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using ApiEjemplo.Models;
using ApiEjemplo.Services;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GrupoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ActivityService _activityService;

        public GrupoController(
            AppDbContext context,
            NotificationService notificationService,
            ActivityService activityService)
        {
            _context = context;
            _notificationService = notificationService;
            _activityService = activityService;
        }

        // =====================================================
        // GET: api/Grupo
        // Lista todos los grupos con conteo de miembros
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var grupos = await _context.Grupos
                .Select(g => new
                {
                    g.grupo_id,
                    g.nombre,
                    g.estatus,
                    g.fecha_creacion,
                    g.monto,
                    g.tasa_interes,
                    g.plazo_meses,
                    g.forma_pago,
                    g.tipo_cnbv,
                    g.clasificacion,
                    g.destino,
                    num_miembros = g.Prestamos.Count(),
                    prestamo_ids = g.Prestamos.Select(p => p.prestamo_id).ToList(),
                })
                .OrderByDescending(g => g.grupo_id)
                .ToListAsync();

            return Ok(grupos);
        }

        // =====================================================
        // GET: api/Grupo/{id}
        // Obtiene un grupo con sus miembros y préstamos
        // =====================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var grupo = await _context.Grupos
                .Include(g => g.Prestamos)
                    .ThenInclude(p => p.Cliente)
                        .ThenInclude(c => c.Usuario)
                .FirstOrDefaultAsync(g => g.grupo_id == id);

            if (grupo == null)
                return NotFound("Grupo no encontrado");

            var result = new
            {
                grupo.grupo_id,
                grupo.nombre,
                grupo.estatus,
                grupo.fecha_creacion,
                grupo.monto,
                grupo.tasa_interes,
                grupo.plazo_meses,
                grupo.forma_pago,
                grupo.tipo_cnbv,
                grupo.clasificacion,
                grupo.destino,
                miembros = grupo.Prestamos.Select(p => new
                {
                    p.prestamo_id,
                    p.cliente_id,
                    nombre = p.Cliente?.Usuario != null
                        ? $"{p.Cliente.Usuario.nombre} {p.Cliente.Usuario.apellido}".Trim()
                        : $"Cliente #{p.cliente_id}",
                    p.monto,
                    p.estatus,
                    p.fecha_creacion,
                }).ToList(),
            };

            return Ok(result);
        }

        // =====================================================
        // POST: api/Grupo
        // Crea un grupo y genera préstamo PENDIENTE por miembro
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GrupoCreateDto dto)
        {
            if (dto.cliente_ids == null || dto.cliente_ids.Count < 2)
                return BadRequest("Se requieren al menos 2 miembros para un préstamo grupal.");

            // Verificar que todos los clientes existen
            var clientesExistentes = await _context.Clientes
                .Where(c => dto.cliente_ids.Contains(c.cliente_id))
                .Select(c => c.cliente_id)
                .ToListAsync();

            var noEncontrados = dto.cliente_ids.Except(clientesExistentes).ToList();
            if (noEncontrados.Any())
                return BadRequest($"Clientes no encontrados: {string.Join(", ", noEncontrados)}");

            var fechaCreacion = TimeHelper.GetMexicoTime();

            // ==============================
            // Cálculos financieros comunes
            // ==============================
            decimal montoTotal = dto.monto + (dto.monto * dto.tasa_interes / 100);
            decimal freqDivGrp = dto.forma_pago switch {
                FormasPago.SEMANAL    => 4m,
                FormasPago.QUINCENAL  => 2m,
                FormasPago.CATORCENAL => 2m,
                _                     => 1m,
            };
            decimal interesPerPeriodoGrp = dto.monto * dto.tasa_interes / (freqDivGrp * 100m);
            decimal ivaPerPeriodoGrp     = interesPerPeriodoGrp * 0.16m;
            decimal pagoMes              = Math.Round(dto.monto / dto.plazo_meses + interesPerPeriodoGrp + ivaPerPeriodoGrp, 2);
            DateTime fechaInicio = fechaCreacion.AddMonths(1);
            DateTime fechaFin    = fechaInicio.AddMonths(dto.plazo_meses - 1);
            decimal moraDiaria   = dto.moratorio_por_dia.HasValue && dto.moratorio_por_dia.Value > 0
                ? dto.moratorio_por_dia.Value
                : Math.Round((pagoMes * 0.10m) / 30m, 2);

            // ==============================
            // Crear el grupo
            // ==============================
            var grupo = new Grupo
            {
                nombre           = dto.nombre,
                fecha_creacion   = fechaCreacion,
                estatus          = "ACTIVO",
                clasificacion    = dto.clasificacion,
                monto            = dto.monto,
                tasa_interes     = dto.tasa_interes,
                plazo_meses      = dto.plazo_meses,
                forma_pago       = dto.forma_pago,
                tipo_cnbv            = dto.tipo_cnbv,
                tb_interes_normal    = dto.tb_interes_normal,
                tipo_tasa            = dto.tipo_tasa,
                tb_interes_moratorio = dto.tb_interes_moratorio,
                tipo_tasa_moratorio  = dto.tipo_tasa_moratorio,
                moratorio_por_dia    = moraDiaria,
                destino              = dto.destino,
            };

            _context.Grupos.Add(grupo);
            await _context.SaveChangesAsync(); // para obtener grupo_id

            // ==============================
            // Crear un préstamo por miembro
            // ==============================
            var prestamos = new List<Prestamo>();
            foreach (var clienteId in dto.cliente_ids)
            {
                var prestamo = new Prestamo
                {
                    cliente_id   = clienteId,
                    grupo_id     = grupo.grupo_id,
                    monto        = dto.monto,
                    tasa_interes = dto.tasa_interes,
                    plazo_meses  = dto.plazo_meses,
                    monto_total  = montoTotal,
                    saldo_actual = montoTotal,
                    pago_mes     = pagoMes,
                    fecha_creacion       = fechaCreacion,
                    fecha_inicio         = fechaInicio,
                    fecha_fin            = fechaFin,
                    fecha_proximo_pago   = fechaInicio,
                    mora_diaria          = moraDiaria,
                    dias_gracia          = dto.dias_gracia,
                    forma_pago           = dto.forma_pago,
                    tipo_cnbv            = dto.tipo_cnbv,
                    tb_interes_normal    = dto.tb_interes_normal,
                    tipo_tasa            = dto.tipo_tasa,
                    tb_interes_moratorio = dto.tb_interes_moratorio,
                    tipo_tasa_moratorio  = dto.tipo_tasa_moratorio,
                    destino              = dto.destino,
                    estatus              = EstatusPrestamo.PENDIENTE,
                };
                prestamos.Add(prestamo);
                _context.Prestamos.Add(prestamo);
            }

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                1,
                $"Grupo \"{grupo.nombre}\" creado con {prestamos.Count} miembros",
                NotificationLevel.POSITIVE
            );

            return Ok(new
            {
                grupo.grupo_id,
                grupo.nombre,
                grupo.estatus,
                grupo.fecha_creacion,
                grupo.monto,
                grupo.plazo_meses,
                grupo.forma_pago,
                num_miembros = prestamos.Count,
                prestamos = prestamos.Select(p => new
                {
                    p.prestamo_id,
                    p.cliente_id,
                    p.monto,
                    p.estatus,
                }).ToList(),
            });
        }
    }
}
