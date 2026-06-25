using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using ApiEjemplo.Services;
using ApiEjemplo.DTOs.Prestamo;
using System.Security.Claims;

namespace ApiEjemplo.Controllers
{
    // MONEYPINE-FIX: DTO mínimo para el body de PATCH /aprobar
    public class AprobarBodyDto
    {
        public string? administrado_en { get; set; }
        // MONEYPINE-FIX: fecha primer pago capturada en modal de apertura (opcional)
        public DateTime? fecha_apertura { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class PrestamoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ActivityService _activityService;

        // =============================
        // Inyección del DbContext
        // =============================
        public PrestamoController(
        AppDbContext context,
        NotificationService notificationService,
        ActivityService activityService)
    {
        _context = context;
        _notificationService = notificationService;
        _activityService = activityService;
    }

        // =====================================================
        // GET: api/Prestamo
        // Lista préstamos, por defecto solo PENDIENTE
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? estatus = null)
        {
            var query = _context.Prestamos
                .Include(p => p.Cliente)
                    .ThenInclude(c => c.Usuario)
                .Include(p => p.Grupo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(estatus) &&
                Enum.TryParse<EstatusPrestamo>(estatus, true, out var e))
                query = query.Where(p => p.estatus == e);

            // MONEYPINE-FIX: materializar antes del Select para usar NombreHelper en memoria
            var raw = await query
                .OrderByDescending(p => p.prestamo_id)
                .ToListAsync();

            var result = raw.Select(p => new {
                p.prestamo_id,
                p.cliente_id,
                numero_cliente   = p.Cliente?.clave_cliente,
                nombre           = NombreHelper.BuildNombreCliente(
                    p.Cliente?.Usuario?.nombre,
                    p.Cliente?.Usuario?.apellido,
                    p.Cliente?.apellido_materno,
                    p.cliente_id),
                apellido_materno = p.Cliente?.apellido_materno,
                p.monto,
                p.tasa_interes,
                p.plazo_meses,
                p.forma_pago,
                p.estatus,
                p.fecha_creacion,
                p.fecha_inicio,
                p.fecha_fin,
                p.destino,
                tipo_solicitud   = p.grupo_id.HasValue ? "PRÉSTAMO GRUPAL" : "PRÉSTAMO PERSONAL",
                p.grupo_id,
                grupo_nombre     = p.Grupo?.nombre,
                administrado_en  = p.administrado_en ?? "SINF"
            }).ToList();

            return Ok(result);
        }

        // =====================================================
        // PATCH: api/Prestamo/{id}/aprobar
        // Aprueba un préstamo PENDIENTE → ACTIVO
        // =====================================================
        [HttpPatch("{id}/aprobar")]
        public async Task<IActionResult> Aprobar(int id, [FromBody] AprobarBodyDto? body = null)
        {
            var prestamo = await _context.Prestamos.FindAsync(id);
            if (prestamo == null)
                return NotFound("Préstamo no encontrado");
            if (prestamo.estatus != EstatusPrestamo.PENDIENTE)
                return BadRequest("Solo se pueden aprobar préstamos en estado PENDIENTE");

            // MONEYPINE-FIX: seguridad — crédito PENDIENTE no debe tener pagos (invariante de negocio)
            var tienePagos = await _context.Pagos.AnyAsync(p => p.prestamo_id == id);
            if (tienePagos)
                return BadRequest("No se puede aprobar un crédito con pagos aplicados");

            // ================================================================
            // MONEYPINE-FIX: determinar fechaBase para amortización
            // Prioridad:
            //   1. body.fecha_apertura (admin la capturó en modal de apertura)
            //   2. prestamo.fecha_inicio si fue editada manualmente antes de aprobar
            //   3. Fecha actual México (día de aprobación — caso default)
            // ================================================================
            int freqDaysApro = prestamo.forma_pago switch {
                FormasPago.DIARIA      => 1,
                FormasPago.SEMANAL     => 7,
                FormasPago.CATORCENAL  => 14,
                FormasPago.QUINCENAL   => 15,
                _                      => 0, // MENSUAL
            };
            bool esMonthlyApro = freqDaysApro == 0;

            // Valor que hubiera calculado el POST automáticamente (sin edición manual)
            DateTime autoCalcFechaInicio = esMonthlyApro
                ? prestamo.fecha_creacion.AddMonths(1)
                : prestamo.fecha_creacion.AddDays(freqDaysApro);

            // Si la diferencia con el auto-calc supera 1 día → fue editada a mano via PUT/CreditoDetalle
            bool fechaManualmenteFijada =
                Math.Abs((prestamo.fecha_inicio - autoCalcFechaInicio).TotalDays) > 1.0;

            DateTime fechaBase;
            if (body?.fecha_apertura.HasValue == true)
                fechaBase = TimeHelper.ConvertToMexicoTime(body.fecha_apertura.Value);
            else if (fechaManualmenteFijada)
                fechaBase = prestamo.fecha_inicio; // Respeta lo que editó el admin en CreditoDetalle
            else
                fechaBase = TimeHelper.GetMexicoTime(); // Default: día de aprobación en hora México

            // ================================================================
            // MONEYPINE-FIX: actualizar fechas del préstamo con fechaBase
            // ================================================================
            prestamo.fecha_inicio       = fechaBase;
            prestamo.fecha_proximo_pago = fechaBase;
            prestamo.fecha_fin          = esMonthlyApro
                ? fechaBase.AddMonths(prestamo.plazo_meses - 1)
                : fechaBase.AddDays((double)(prestamo.plazo_meses - 1) * freqDaysApro);

            // ================================================================
            // MONEYPINE-FIX: regenerar periodos de amortización
            // Periodo 1: vence en fechaBase (mismo día de apertura)
            // Periodo i: vence en fechaBase + (i-1) × freq
            // ================================================================
            var periodosExistentes = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == id)
                .ToListAsync();
            _context.PeriodosAmortizacion.RemoveRange(periodosExistentes);

            decimal mesesPlazoApro    = prestamo.forma_pago switch {
                FormasPago.DIARIA      => prestamo.plazo_meses / 30m,
                FormasPago.SEMANAL     => prestamo.plazo_meses / 4m,
                FormasPago.QUINCENAL   => prestamo.plazo_meses / 2m,
                FormasPago.CATORCENAL  => prestamo.plazo_meses * (14m / 30m),
                _                      => prestamo.plazo_meses,
            };
            decimal interesTotalApro   = prestamo.monto * (prestamo.tasa_interes / 100m) * mesesPlazoApro;
            decimal ivaRateApro        = (prestamo.iva.HasValue && prestamo.iva > 0) ? prestamo.iva.Value / 100m : 0m;
            decimal ivaTotalApro       = interesTotalApro * ivaRateApro;
            decimal capitalPorPagoApro = Math.Round(prestamo.monto / prestamo.plazo_meses, 2);
            decimal interesPorPagoApro = Math.Round(interesTotalApro / prestamo.plazo_meses, 2);
            decimal ivaPorPagoApro     = Math.Round(ivaTotalApro / prestamo.plazo_meses, 2);
            decimal _pagoBrutoApro     = capitalPorPagoApro + interesPorPagoApro + ivaPorPagoApro;
            decimal pagoRegularApro    = Math.Round(_pagoBrutoApro, 2);

            prestamo.pago_mes    = pagoRegularApro;
            prestamo.monto_total = prestamo.monto + interesTotalApro + ivaTotalApro;
            prestamo.mora_diaria = Math.Round(pagoRegularApro * (prestamo.tasa_interes * 12m * 2m / 100m) / 360m, 2);

            var periodosList = new List<PeriodoAmortizacion>();
            for (int i = 1; i <= prestamo.plazo_meses; i++)
            {
                bool esUltimo = i == prestamo.plazo_meses;

                DateTime fechaVencP = esMonthlyApro
                    ? fechaBase.AddMonths(i - 1)
                    : fechaBase.AddDays((double)(i - 1) * freqDaysApro);
                DateTime fechaInicioP = esMonthlyApro
                    ? fechaBase.AddMonths(i - 2)
                    : fechaBase.AddDays((double)(i - 2) * freqDaysApro);

                decimal capEste = esUltimo
                    ? Math.Round(prestamo.monto - capitalPorPagoApro * (prestamo.plazo_meses - 1), 2)
                    : capitalPorPagoApro;
                decimal intEste = esUltimo
                    ? Math.Round(interesTotalApro - interesPorPagoApro * (prestamo.plazo_meses - 1), 2)
                    : interesPorPagoApro;
                decimal ivaEste = esUltimo
                    ? Math.Round(ivaTotalApro - ivaPorPagoApro * (prestamo.plazo_meses - 1), 2)
                    : ivaPorPagoApro;
                decimal pagoEste = capEste + intEste + ivaEste;
                if (pagoEste % 1 > 0.001m) pagoEste = Math.Ceiling(pagoEste);

                decimal capitalPendiente = prestamo.monto - capitalPorPagoApro * (i - 1);
                decimal saldoFinal       = esUltimo
                    ? 0m
                    : Math.Max(0m, prestamo.monto - capitalPorPagoApro * i);

                periodosList.Add(new PeriodoAmortizacion
                {
                    prestamo_id       = prestamo.prestamo_id,
                    periodo           = i,
                    fecha_inicio      = fechaInicioP,
                    fecha_vencimiento = fechaVencP,
                    capital_pendiente = capitalPendiente,
                    abono_capital     = capEste,
                    interes_normal    = intEste,
                    interes_iva       = ivaEste,
                    pago_pactado      = pagoEste,
                    saldo_final       = saldoFinal,
                    estado_pago       = 1,
                });
            }
            _context.PeriodosAmortizacion.AddRange(periodosList);

            prestamo.estatus = EstatusPrestamo.ACTIVO;
            // MONEYPINE-FIX: guardar sistema fiscal al aprobar el crédito
            if (body?.administrado_en != null)
                prestamo.administrado_en = body.administrado_en;
            await _context.SaveChangesAsync();

            int? userIdAprobar = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedAprobar))
                userIdAprobar = parsedAprobar;

            var nombreClienteAprobar = await _context.Clientes
                .Where(c => c.cliente_id == prestamo.cliente_id)
                .Include(c => c.Usuario)
                .Select(c => c.Usuario != null
                    ? $"{c.Usuario.nombre} {c.Usuario.apellido}"
                    : $"Cliente #{prestamo.cliente_id}")
                .FirstOrDefaultAsync() ?? $"Cliente #{prestamo.cliente_id}";

            await _activityService.CreateActivity(
                ActivityType.CREDIT_APPROVED,
                prestamo.cliente_id,
                prestamo.monto,
                NotificationLevel.POSITIVE,
                description: $"Crédito #{prestamo.prestamo_id} aperturado por ${prestamo.monto:N2} a nombre de {nombreClienteAprobar}",
                userId: userIdAprobar
            );
            await _notificationService.CreateNotification(
                1, "Préstamo #" + id + " aprobado y activado", NotificationLevel.POSITIVE);

            return Ok(prestamo);
        }

        // =====================================================
        // PATCH: api/Prestamo/{id}/cancelar
        // Cancela un préstamo PENDIENTE → CANCELADO
        // =====================================================
        [HttpPatch("{id}/cancelar")]
        public async Task<IActionResult> Cancelar(int id)
        {
            var prestamo = await _context.Prestamos.FindAsync(id);
            if (prestamo == null)
                return NotFound("Préstamo no encontrado");
            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                return BadRequest("No se puede cancelar un préstamo liquidado");

            prestamo.estatus = EstatusPrestamo.CANCELADO;
            await _context.SaveChangesAsync();

            int? userIdCancelar = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedCancelar))
                userIdCancelar = parsedCancelar;

            await _activityService.CreateActivity(
                ActivityType.CUSTOM,
                prestamo.cliente_id,
                prestamo.saldo_actual,
                NotificationLevel.NEUTRAL,
                description: $"Crédito #{prestamo.prestamo_id} marcado como cancelado",
                userId: userIdCancelar
            );
            await _notificationService.CreateNotification(
                1, "Préstamo #" + id + " cancelado", NotificationLevel.NEUTRAL);

            return Ok(prestamo);
        }

        // =====================================================
        // GET: api/Prestamo/cliente/5
        // Obtiene todos los préstamos de un cliente
        // =====================================================
        [HttpGet("cliente/{clienteId}")]
        public async Task<IActionResult> GetByCliente(int clienteId)
        {
            var prestamos = await _context.Prestamos
                .Where(p => p.cliente_id == clienteId)
                .Include(p => p.Pagos)
                .ToListAsync();

            return Ok(prestamos);
        }

        // =====================================================
        // GET: api/Prestamo/5
        // Obtiene un préstamo por su ID (incluye datos del cliente)
        // =====================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPrestamo(int id)
        {
            var p = await _context.Prestamos
                .Include(x => x.Pagos)
                .Include(x => x.Cliente)
                    .ThenInclude(c => c.Usuario)
                .Include(x => x.Grupo)
                .FirstOrDefaultAsync(x => x.prestamo_id == id);

            // MONEYPINE-FIX: cargar avales del préstamo con nombre de cada garante
            var avalesData = await _context.PrestamosAvales
                .Where(a => a.prestamo_id == id)
                .Include(a => a.Aval).ThenInclude(c => c!.Usuario)
                .Select(a => new {
                    cliente_id_aval = a.cliente_id_aval,
                    nombre = a.Aval != null && a.Aval.Usuario != null
                        ? ((a.Aval.Usuario.nombre ?? "") + " " + (a.Aval.Usuario.apellido ?? "")).Trim()
                        : "Aval #" + a.cliente_id_aval
                })
                .ToListAsync();

            if (p == null)
                return NotFound("Préstamo no encontrado");

            // Otros miembros del mismo grupo (solo si es préstamo grupal)
            // MONEYPINE-FIX: materializar primero para usar NombreHelper client-side (EF no soporta métodos custom en expression trees)
            var miembros_raw = p.grupo_id.HasValue
                ? await _context.Prestamos
                    .Where(x => x.grupo_id == p.grupo_id && x.prestamo_id != p.prestamo_id)
                    .Include(x => x.Cliente).ThenInclude(c => c.Usuario)
                    .Select(x => new {
                        x.prestamo_id,
                        x.cliente_id,
                        usuNombre   = x.Cliente != null && x.Cliente.Usuario != null ? x.Cliente.Usuario.nombre   : null,
                        usuApellido = x.Cliente != null && x.Cliente.Usuario != null ? x.Cliente.Usuario.apellido : null,
                        apMat       = x.Cliente != null ? x.Cliente.apellido_materno : null,
                    })
                    .ToListAsync()
                : null;

            var miembros_grupo = miembros_raw?
                .Select(x => (object)new {
                    x.prestamo_id,
                    nombre = NombreHelper.BuildNombreCliente(x.usuNombre, x.usuApellido, x.apMat, x.cliente_id),
                })
                .ToList<object>();

            // MONEYPINE-FIX: calcular mora acumulada actual segun dias de atraso
            decimal moraAcumuladaActual = 0;
            if (p.fecha_proximo_pago.HasValue && p.saldo_actual > 0)
            {
                var fechaLimite = p.fecha_proximo_pago.Value.AddDays(p.dias_gracia);
                if (DateTime.UtcNow > fechaLimite)
                {
                    int diasAtraso = (int)(DateTime.UtcNow - p.fecha_proximo_pago.Value).TotalDays;
                    moraAcumuladaActual = diasAtraso * p.mora_diaria;
                }
            }

            // MONEYPINE-FIX: totales de pagos para la consulta de movimientos
            var totalPagado   = p.Pagos.Sum(x => x.monto_pagado);
            var totalMoraPagada = p.Pagos.Sum(x => x.mora_pagada);
            var pagosRealizados = p.Pagos.Count;

            // MONEYPINE-FIX: usar pago_mes real desde primer período (evita error de Math.Ceiling en BD legada)
            var primerPeriodo = await _context.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == id)
                .OrderBy(pa => pa.periodo)
                .Select(pa => new { pa.abono_capital, pa.interes_normal, pa.interes_iva })
                .FirstOrDefaultAsync();
            decimal pagoMesReal = primerPeriodo != null
                ? Math.Round(primerPeriodo.abono_capital + primerPeriodo.interes_normal + primerPeriodo.interes_iva, 2)
                : p.pago_mes;

            return Ok(new {
                p.prestamo_id,
                p.cliente_id,
                p.monto,
                p.monto_total,
                p.tasa_interes,
                p.plazo_meses,
                p.forma_pago,
                p.estatus,
                p.fecha_creacion,
                p.fecha_inicio,
                p.fecha_fin,
                p.fecha_proximo_pago,
                pago_mes = pagoMesReal,
                p.mora_diaria,
                p.tasa_moratorio_anual,
                p.saldo_actual,
                p.dias_gracia,
                mora_acumulada   = moraAcumuladaActual,
                total_pagado     = totalPagado,
                total_mora_pagada = totalMoraPagada,
                pagos_realizados = pagosRealizados,
                p.tipo_cnbv,
                p.tb_interes_normal,
                p.tipo_tasa,
                p.tb_interes_moratorio,
                p.tipo_tasa_moratorio,
                p.destino,
                p.cobrador_id,
                tipo_solicitud  = p.grupo_id.HasValue ? "PRÉSTAMO GRUPAL" : "PRÉSTAMO PERSONAL",
                p.grupo_id,
                grupo_nombre    = p.Grupo != null ? p.Grupo.nombre : null,
                miembros_grupo,
                // MONEYPINE-FIX: usar NombreHelper para evitar duplicar apellido materno en clientes legacy
                nombre_cliente  = p.Cliente != null
                    ? NombreHelper.BuildNombreCliente(
                        p.Cliente.Usuario?.nombre,
                        p.Cliente.Usuario?.apellido,
                        p.Cliente.apellido_materno,
                        p.cliente_id)
                    : "Cliente #" + p.cliente_id,
                ruta_vinculacion = p.Cliente != null ? p.Cliente.ruta_vinculacion : null, // MONEYPINE-FIX: exponer ruta del cliente en detalle de crédito
                curp             = p.Cliente != null ? p.Cliente.curp                : null,
                rfc              = p.Cliente != null ? p.Cliente.rfc                 : null,
                telefono         = p.Cliente != null ? p.Cliente.telefono_particular : null,
                direccion        = p.Cliente != null ? p.Cliente.direccion           : null,
                colonia          = p.Cliente != null ? p.Cliente.colonia             : null,
                // MONEYPINE-FIX: campos de domicilio extendidos para edición en CreditoDetalle
                usuario_id       = p.Cliente != null ? p.Cliente.usuario_id          : (int?)null,
                cp               = p.Cliente != null ? p.Cliente.cp                  : null,
                estado_domicilio = p.Cliente != null ? p.Cliente.estado_domicilio    : null,
                municipio        = p.Cliente != null ? p.Cliente.municipio           : null,
                num_ext          = p.Cliente != null ? p.Cliente.num_ext             : null,
                avales           = avalesData, // MONEYPINE-FIX: garantes del préstamo
                administrado_en  = p.administrado_en ?? "SINF", // MONEYPINE-FIX: sistema fiscal del crédito
            });
        }

        // =====================================================
        // POST: api/Prestamo
        // Crea un nuevo préstamo (usa DTO)
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PrestamoCreateDTO dto)
        {
            var clienteExiste = await _context.Clientes
                .AnyAsync(c => c.cliente_id == dto.cliente_id);

            if (!clienteExiste)
                return BadRequest("El cliente no existe");

            DateTime fechaCreacion;

            if (dto.fecha_creacion.HasValue)
            {
                fechaCreacion = TimeHelper.ConvertToMexicoTime(dto.fecha_creacion.Value);
            }
            else
            {
                fechaCreacion = TimeHelper.GetMexicoTime();
            }

            // ================================
            // CÁLCULOS FINANCIEROS
            // ================================

            decimal mesesPlazo     = dto.forma_pago switch {
                FormasPago.DIARIA      => dto.plazo_meses / 30m,
                FormasPago.SEMANAL     => dto.plazo_meses / 4m,
                FormasPago.QUINCENAL   => dto.plazo_meses / 2m,
                FormasPago.CATORCENAL  => dto.plazo_meses * (14m / 30m),
                _                      => dto.plazo_meses,
            };
            decimal interesTotal   = dto.monto * (dto.tasa_interes / 100m) * mesesPlazo;
            decimal ivaRate        = (dto.iva.HasValue && dto.iva > 0) ? dto.iva.Value / 100m : 0m;
            decimal ivaTotal       = interesTotal * ivaRate;
            decimal capitalPorPago = Math.Round(dto.monto / dto.plazo_meses, 2);
            decimal interesPorPago = Math.Round(interesTotal / dto.plazo_meses, 2);
            decimal ivaPorPago     = Math.Round(ivaTotal / dto.plazo_meses, 2);
            decimal _pagoBruto     = capitalPorPago + interesPorPago + ivaPorPago;
            decimal pagoMes        = Math.Round(_pagoBruto, 2);
            decimal montoTotal     = dto.monto + interesTotal + ivaTotal;

            DateTime fechaInicio =
                fechaCreacion.AddMonths(1);

            DateTime fechaFin =
                fechaInicio.AddMonths(dto.plazo_meses - 1);

            decimal moraDiaria = Math.Round(pagoMes * (dto.tasa_interes * 12m * 2m / 100m) / 360m, 2);

            var prestamo = new Prestamo
            {
                cliente_id = dto.cliente_id,
                monto = dto.monto,
                tasa_interes = dto.tasa_interes,
                plazo_meses = dto.plazo_meses,
                fecha_creacion = fechaCreacion,
                dias_gracia = dto.dias_gracia,

                monto_total = montoTotal,

                // SALDO INICIAL (capital puro, sin interés)
                saldo_actual = dto.monto,

                pago_mes = pagoMes,

                fecha_inicio = fechaInicio,
                fecha_fin = fechaFin,
                fecha_proximo_pago = fechaInicio,

                mora_diaria = moraDiaria,

                forma_pago = dto.forma_pago,
                cobrador_id = dto.cobrador_id,

                // Campos CNBV / producto
                tipo_cnbv            = dto.tipo_cnbv,
                iva                  = dto.iva,
                tb_interes_normal    = dto.tb_interes_normal,
                tipo_tasa            = dto.tipo_tasa,
                tb_interes_moratorio = dto.tb_interes_moratorio,
                tipo_tasa_moratorio  = dto.tipo_tasa_moratorio,
                destino              = dto.destino,
                administrado_en      = dto.administrado_en ?? "SINF", // MONEYPINE-FIX: sistema fiscal

                estatus = EstatusPrestamo.PENDIENTE
            };

            _context.Prestamos.Add(prestamo);
            await _context.SaveChangesAsync();

            // MONEYPINE-FIX: guardar avales (clientes garantes) del préstamo
            if (dto.avales != null && dto.avales.Count > 0)
            {
                foreach (var clienteAvalId in dto.avales)
                {
                    var avalExiste = await _context.Clientes.AnyAsync(c => c.cliente_id == clienteAvalId);
                    if (avalExiste)
                    {
                        _context.PrestamosAvales.Add(new PrestamoAval
                        {
                            prestamo_id    = prestamo.prestamo_id,
                            cliente_id_aval = clienteAvalId,
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Los períodos se generan al APROBAR el crédito, no al crearlo.

            await _activityService.CreateActivity(
            ActivityType.CREDIT_APPROVED,
            prestamo.cliente_id,
            prestamo.monto,
            NotificationLevel.NEUTRAL
        );

            await _notificationService.CreateNotification(
                1, // temporal hasta que tengas usuario autenticado
                "Préstamo creado correctamente",
                NotificationLevel.POSITIVE
            );

            return CreatedAtAction(
                nameof(GetPrestamo),
                new { id = prestamo.prestamo_id },
                prestamo
            );
        }



        // =====================================================
        // PUT: api/Prestamo/5
        // Actualiza datos permitidos de un préstamo
        // =====================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] PrestamoUpdateDTO dto)
        {
            var prestamo = await _context.Prestamos
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.prestamo_id == id);

            if (prestamo == null)
                return NotFound("Préstamo no encontrado");

            // ================================
            // ACTUALIZAR CAMPOS PERMITIDOS
            // ================================

            prestamo.monto = dto.monto;
            prestamo.plazo_meses = dto.plazo_meses;
            prestamo.estatus = dto.estatus;
            prestamo.forma_pago = dto.forma_pago;

            // Comisión y seguro
            prestamo.comision_apertura = dto.comision_apertura;
            prestamo.seguro_credito    = dto.seguro_credito;

            // Ingresos
            prestamo.ing_semanal = dto.ing_semanal;
            prestamo.ing_otros   = dto.ing_otros;
            prestamo.ing_total   = dto.ing_total;

            // Gastos
            prestamo.g_alimento   = dto.g_alimento;
            prestamo.g_luz        = dto.g_luz;
            prestamo.g_telefono   = dto.g_telefono;
            prestamo.g_transporte = dto.g_transporte;
            prestamo.g_renta      = dto.g_renta;
            prestamo.g_inversion  = dto.g_inversion;
            prestamo.g_creditos   = dto.g_creditos;
            prestamo.g_otros      = dto.g_otros;
            prestamo.total_gasto      = dto.total_gasto;
            prestamo.total_disponible = dto.total_disponible;

            // Cuenta desembolso
            prestamo.nombre_banco = dto.nombre_banco;
            prestamo.num_cuenta   = dto.num_cuenta;

            // MONEYPINE-FIX: fecha de creación editable
            if (dto.fecha_creacion.HasValue)
                prestamo.fecha_creacion = dto.fecha_creacion.Value;

            // MONEYPINE-FIX: sistema fiscal
            if (dto.administrado_en != null)
                prestamo.administrado_en = dto.administrado_en;

            // Ruta vinculada → actualizar el Cliente asociado
            if (prestamo.Cliente != null && dto.ruta_vinculacion != null)
                prestamo.Cliente.ruta_vinculacion = dto.ruta_vinculacion;

            // ================================
            // RECALCULAR DATOS FINANCIEROS
            // ================================

            decimal mesesPlazoPut  = prestamo.forma_pago switch {
                FormasPago.DIARIA      => prestamo.plazo_meses / 30m,
                FormasPago.SEMANAL     => prestamo.plazo_meses / 4m,
                FormasPago.QUINCENAL   => prestamo.plazo_meses / 2m,
                FormasPago.CATORCENAL  => prestamo.plazo_meses * (14m / 30m),
                _                      => prestamo.plazo_meses,
            };
            decimal interesTotalPut  = prestamo.monto * (prestamo.tasa_interes / 100m) * mesesPlazoPut;
            decimal ivaRatePut       = (prestamo.iva.HasValue && prestamo.iva > 0) ? prestamo.iva.Value / 100m : 0m;
            decimal ivaTotalPut      = interesTotalPut * ivaRatePut;
            decimal capPorPagoPut    = Math.Round(prestamo.monto / prestamo.plazo_meses, 2);
            decimal intPorPagoPut    = Math.Round(interesTotalPut / prestamo.plazo_meses, 2);
            decimal ivaPorPagoPut    = Math.Round(ivaTotalPut / prestamo.plazo_meses, 2);
            decimal _pagoBrutoPut    = capPorPagoPut + intPorPagoPut + ivaPorPagoPut;
            prestamo.pago_mes        = Math.Round(_pagoBrutoPut, 2);
            prestamo.monto_total     = prestamo.monto + interesTotalPut + ivaTotalPut;

            // SALDO INICIAL (capital puro, sin interés)
            prestamo.saldo_actual = prestamo.monto;

            // MONEYPINE-FIX: calcular desplazamiento de fechas según forma_pago (igual que GenerarPeriodos)
            int freqDaysUpd = prestamo.forma_pago switch {
                FormasPago.DIARIA      => 1,
                FormasPago.SEMANAL     => 7,
                FormasPago.CATORCENAL  => 14,
                FormasPago.QUINCENAL   => 15,
                _                      => 0, // MENSUAL
            };
            bool esMonthlyUpd = freqDaysUpd == 0;

            // MONEYPINE-FIX: fecha_inicio puede venir explícita del DTO (campo "Fecha primer pago")
            if (dto.fecha_inicio.HasValue)
                prestamo.fecha_inicio = dto.fecha_inicio.Value;
            else
                prestamo.fecha_inicio = esMonthlyUpd
                    ? prestamo.fecha_creacion.AddMonths(1)
                    : prestamo.fecha_creacion.AddDays(freqDaysUpd);

            prestamo.fecha_fin = esMonthlyUpd
                ? prestamo.fecha_inicio.AddMonths(prestamo.plazo_meses - 1)
                : prestamo.fecha_inicio.AddDays((double)(prestamo.plazo_meses - 1) * freqDaysUpd);

            prestamo.fecha_proximo_pago = prestamo.fecha_inicio;

            prestamo.mora_diaria =
                Math.Round(prestamo.pago_mes * (prestamo.tasa_interes * 12m * 2m / 100m) / 360m, 2);

            // MONEYPINE-FIX: actualizar fechas de periodos usando fecha_inicio como ancla
            // Periodo i: vence en fecha_inicio + (i-1)*freq; inicia en fecha_inicio + (i-2)*freq
            var periodosExistentes = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == id)
                .OrderBy(p => p.periodo)
                .ToListAsync();

            if (periodosExistentes.Any())
            {
                foreach (var p in periodosExistentes)
                {
                    p.fecha_vencimiento = esMonthlyUpd
                        ? prestamo.fecha_inicio.AddMonths(p.periodo - 1)
                        : prestamo.fecha_inicio.AddDays((double)(p.periodo - 1) * freqDaysUpd);
                    p.fecha_inicio = esMonthlyUpd
                        ? prestamo.fecha_inicio.AddMonths(p.periodo - 2)
                        : prestamo.fecha_inicio.AddDays((double)(p.periodo - 2) * freqDaysUpd);
                    _context.PeriodosAmortizacion.Update(p);
                }
            }

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                1,
                "Préstamo actualizado",
                NotificationLevel.NEUTRAL
            );

            return NoContent();
        }

        // =====================================================
        // POST: api/Prestamo/{id}/generar-periodos
        // Genera la tabla de amortización para un crédito que no tiene períodos
        // =====================================================
        [HttpPost("{id}/generar-periodos")]
        public async Task<IActionResult> GenerarPeriodos(int id)
        {
            var prestamo = await _context.Prestamos.FindAsync(id);
            if (prestamo == null)
                return NotFound("Préstamo no encontrado");

            var yaExisten = await _context.PeriodosAmortizacion
                .AnyAsync(p => p.prestamo_id == id);
            if (yaExisten)
                return BadRequest("Este préstamo ya tiene períodos de amortización generados.");

            int freqDays = prestamo.forma_pago switch {
                FormasPago.DIARIA      => 1,
                FormasPago.SEMANAL     => 7,
                FormasPago.CATORCENAL  => 14,
                FormasPago.QUINCENAL   => 15,
                _                      => 0, // MENSUAL
            };
            bool esMonthly = freqDays == 0;

            decimal mesesPlazoGen    = prestamo.forma_pago switch {
                FormasPago.DIARIA      => prestamo.plazo_meses / 30m,
                FormasPago.SEMANAL     => prestamo.plazo_meses / 4m,
                FormasPago.QUINCENAL   => prestamo.plazo_meses / 2m,
                FormasPago.CATORCENAL  => prestamo.plazo_meses * (14m / 30m),
                _                      => prestamo.plazo_meses,
            };
            decimal interesTotalGen   = prestamo.monto * (prestamo.tasa_interes / 100m) * mesesPlazoGen;
            decimal ivaRateGen        = (prestamo.iva.HasValue && prestamo.iva > 0) ? prestamo.iva.Value / 100m : 0m;
            decimal ivaTotalGen       = interesTotalGen * ivaRateGen;
            decimal capitalPorPagoGen = Math.Round(prestamo.monto / prestamo.plazo_meses, 2);
            decimal interesPorPagoGen = Math.Round(interesTotalGen / prestamo.plazo_meses, 2);
            decimal ivaPorPagoGen     = Math.Round(ivaTotalGen / prestamo.plazo_meses, 2);
            decimal _pagoBrutoGen     = capitalPorPagoGen + interesPorPagoGen + ivaPorPagoGen;
            decimal pagoRegularGen    = _pagoBrutoGen % 1 > 0.001m ? Math.Ceiling(_pagoBrutoGen) : _pagoBrutoGen;

            DateTime fechaBase = prestamo.fecha_creacion;

            var periodosList = new List<PeriodoAmortizacion>();
            for (int i = 1; i <= prestamo.plazo_meses; i++)
            {
                bool esUltimo = i == prestamo.plazo_meses;

                DateTime fechaInicioP = esMonthly
                    ? fechaBase.AddMonths(i - 1)
                    : fechaBase.AddDays((double)(i - 1) * freqDays);
                DateTime fechaVencP = esMonthly
                    ? fechaBase.AddMonths(i)
                    : fechaBase.AddDays((double)i * freqDays);

                decimal capEste = esUltimo
                    ? Math.Round(prestamo.monto - capitalPorPagoGen * (prestamo.plazo_meses - 1), 2)
                    : capitalPorPagoGen;
                decimal intEste = esUltimo
                    ? Math.Round(interesTotalGen - interesPorPagoGen * (prestamo.plazo_meses - 1), 2)
                    : interesPorPagoGen;
                decimal ivaEste = esUltimo
                    ? Math.Round(ivaTotalGen - ivaPorPagoGen * (prestamo.plazo_meses - 1), 2)
                    : ivaPorPagoGen;
                decimal pagoEste = capEste + intEste + ivaEste;
                if (pagoEste % 1 > 0.001m) pagoEste = Math.Ceiling(pagoEste);

                decimal capitalPendiente = prestamo.monto - capitalPorPagoGen * (i - 1);
                decimal saldoFinal       = esUltimo ? 0m : Math.Max(0m, prestamo.monto - capitalPorPagoGen * i);

                periodosList.Add(new PeriodoAmortizacion
                {
                    prestamo_id       = prestamo.prestamo_id,
                    periodo           = i,
                    fecha_inicio      = fechaInicioP,
                    fecha_vencimiento = fechaVencP,
                    capital_pendiente = capitalPendiente,
                    abono_capital     = capEste,
                    interes_normal    = intEste,
                    interes_iva       = ivaEste,
                    pago_pactado      = pagoEste,
                    saldo_final       = saldoFinal,
                    estado_pago       = 1,
                });
            }

            _context.PeriodosAmortizacion.AddRange(periodosList);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"{periodosList.Count} períodos generados para préstamo #{id}.", total = periodosList.Count });
        }

        // =====================================================
        // DELETE: api/Prestamo/5
        // Elimina un préstamo (si no está liquidado)
        // =====================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var prestamo = await _context.Prestamos.FindAsync(id);

            if (prestamo == null)
                return NotFound("Préstamo no encontrado");

            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                return BadRequest("No se puede eliminar un préstamo liquidado");

            _context.Prestamos.Remove(prestamo);
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                1,
                "Préstamo eliminado",
                NotificationLevel.NEUTRAL
            );

            return NoContent();
        }

        // =====================================================
        // GET: api/Prestamo/{id}/amortizacion
        // MONEYPINE-FIX: devuelve períodos desde BD; recalcula interesMoratorio
        //   solo para períodos RETRASO (estado_pago=1 vencido).
        //   Períodos con mora congelada (estado_pago=5) conservan el valor guardado.
        // =====================================================
        [HttpGet("{id}/amortizacion")]
        public async Task<IActionResult> GetAmortizacion(int id)
        {
            var prestamo = await _context.Prestamos.FindAsync(id);
            if (prestamo == null)
                return NotFound("Préstamo no encontrado");

            var periodos = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == id)
                .OrderBy(p => p.periodo)
                .ToListAsync();

            if (!periodos.Any())
                return NotFound("No hay períodos de amortización para este préstamo");

            var hoy = DateTime.UtcNow.Date;
            var isLiquidado = prestamo.estatus == EstatusPrestamo.LIQUIDADO;

            int freqDaysFromFormaPago = prestamo.forma_pago switch {
                FormasPago.DIARIA      => 1,
                FormasPago.SEMANAL     => 7,
                FormasPago.CATORCENAL  => 14,
                FormasPago.QUINCENAL   => 15,
                FormasPago.MENSUAL     => 30,
                _                     => 7,
            };
            int freqDaysFromPeriodos = periodos.Count > 1
                ? periodos
                    .Zip(periodos.Skip(1), (a, b) => Math.Abs((int)(b.fecha_vencimiento.Date - a.fecha_vencimiento.Date).TotalDays))
                    .Where(days => days > 0)
                    .GroupBy(days => days)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Select(g => g.Key)
                    .FirstOrDefault()
                : 0;
            int freqDays = freqDaysFromPeriodos > 0 ? freqDaysFromPeriodos : freqDaysFromFormaPago;

            // Acumulado real de cap/int/iva ya aplicado por período (desde pago_detalle de pagos APLICADOS)
            var acumPd = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == id && pd.periodo_id != null)
                .Join(_context.Pagos.Where(pg => pg.estatus == EstatusPago.APLICADO),
                      pd => pd.pago_id, pg => pg.pago_id, (pd, _) => pd)
                .GroupBy(pd => pd.periodo_id!.Value)
                .Select(g => new {
                    pid    = g.Key,
                    capPag = g.Sum(x => x.capital_aplicado),
                    intPag = g.Sum(x => x.interes_aplicado),
                    ivaPag = g.Sum(x => x.iva_aplicado),
                })
                .ToDictionaryAsync(x => x.pid);

            var result = periodos.Select(p =>
            {
                string estadoPago;
                decimal interesMoratorio;
                int diasMoratorio;

                bool esPagado   = p.estado_pago == 2 || p.estado_pago == 3;
                bool esCongelado = p.estado_pago == 5;
                var  fechaVenc  = p.fecha_vencimiento.Date;

                if (isLiquidado || esPagado)
                {
                    // MONEYPINE-FIX: período pagado — mora = 0
                    estadoPago       = "PAGADO";
                    interesMoratorio = 0;
                    diasMoratorio    = 0;
                }
                else if (esCongelado)
                {
                    // MONEYPINE-FIX: mora congelada — mostrar mora neta restando lo ya pagado via solo_mora
                    decimal moraCongeladaNeta = Math.Max(0m, p.interes_moratorio - p.ahorro_por_pago);
                    if (moraCongeladaNeta <= 0)
                    {
                        estadoPago       = "PAGADO"; // Mora totalmente saldada via solo_mora
                        interesMoratorio = 0;
                        diasMoratorio    = p.dias_moratorio;
                    }
                    else
                    {
                        estadoPago       = "CONGELADO";
                        interesMoratorio = moraCongeladaNeta;
                        diasMoratorio    = p.dias_moratorio;
                    }
                }
                else if (fechaVenc <= hoy) // MONEYPINE-FIX: <= incluye periodos que vencen hoy
                {
                    // MONEYPINE-FIX: vencido sin pagar — recalcular moratorio sobre capital insoluto
                    //   formula: capitalPendiente × (tasaAnualMoratorio/100/365) × diasMora
                    estadoPago    = "RETRASO";
                    diasMoratorio = (int)(hoy - fechaVenc).TotalDays;
                    decimal moraCalculada = diasMoratorio > 0 && prestamo.mora_diaria > 0 // MONEYPINE-FIX: usa mora_diaria directa (tasa_moratorio_anual = 0 en migración)
                        ? Math.Round(prestamo.mora_diaria * diasMoratorio, 2)
                        : 0;
                    interesMoratorio = Math.Max(0m, moraCalculada - p.ahorro_por_pago); // Restar mora ya pagada vía solo_mora
                }
                else
                {
                    // Próximo período o futuro
                    estadoPago       = fechaVenc <= hoy.AddDays(freqDays) ? "PENDIENTE" : "INACTIVO";
                    interesMoratorio = 0;
                    diasMoratorio    = 0;
                }

                var interesNormal  = p.interes_normal;
                var interesIVA_val = p.interes_iva;
                var interes        = interesNormal + interesIVA_val;
                var pagoProgramado = p.abono_capital + interes;

                // Cantidad real aún pendiente de pagar (descuenta lo ya abonado via pago_detalle)
                decimal pagoRestante;
                if (estadoPago == "PAGADO")
                {
                    pagoRestante = 0m;
                }
                else
                {
                    var ac = acumPd.GetValueOrDefault(p.periodo_id);
                    decimal capRest = Math.Max(0m, p.abono_capital  - (ac?.capPag ?? 0m));
                    decimal intRest = Math.Max(0m, p.interes_normal - (ac?.intPag ?? 0m));
                    decimal ivaRest = Math.Max(0m, p.interes_iva    - (ac?.ivaPag ?? 0m));
                    pagoRestante = capRest + intRest + ivaRest;
                }

                return new {
                    periodo          = p.periodo,
                    fechaPago        = p.fecha_vencimiento.ToString("yyyy-MM-dd"),
                    saldoPendiente   = p.capital_pendiente + interes + interesMoratorio,
                    capitalPendiente = p.capital_pendiente,
                    abonoCapital     = p.abono_capital,
                    interes          = interesNormal,
                    interesIVA       = interesIVA_val,
                    interesMoratorio,
                    diasMoratorio,
                    saldoFinal       = p.saldo_final,
                    pagoProgramado,
                    pagoRestante,                                               // restante real de cap+int+iva (parciales)
                    pagoTotal        = pagoProgramado + interesMoratorio,
                    pagoTotalVisual  = pagoProgramado,
                    pagoPactado      = p.pago_pactado,
                    estadoPago,
                };
            }).ToList();

            return Ok(result);
        }

        // =====================================================
        // GET: api/Prestamo/comisiones
        // Devuelve préstamos con comisión calculada por asesor
        // =====================================================
        [HttpGet("comisiones")]
        public async Task<IActionResult> GetComisiones(
            [FromQuery] DateTime? desde      = null,
            [FromQuery] DateTime? hasta      = null,
            [FromQuery] int?      cobrador_id = null,
            [FromQuery] decimal   porcentaje  = 3.2m)
        {
            var query = _context.Prestamos
                .Include(p => p.Cliente).ThenInclude(c => c.Usuario)
                .Where(p => p.estatus != EstatusPrestamo.PENDIENTE && p.estatus != EstatusPrestamo.CANCELADO)
                .AsQueryable();

            if (desde.HasValue)
                query = query.Where(p => p.fecha_inicio >= desde.Value || p.fecha_creacion >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(p => p.fecha_inicio <= hasta.Value.AddDays(1));
            if (cobrador_id.HasValue)
                query = query.Where(p => p.cobrador_id == cobrador_id.Value);

            var prestamos = await query.OrderByDescending(p => p.fecha_inicio).ToListAsync();

            var cobradorIds = prestamos.Where(p => p.cobrador_id.HasValue).Select(p => p.cobrador_id!.Value).Distinct().ToList();
            var cobradores  = await _context.Usuarios
                .Where(u => cobradorIds.Contains(u.usuario_id))
                .ToDictionaryAsync(u => u.usuario_id, u => $"{u.nombre} {u.apellido}".Trim());

            var idx    = 1;
            var result = prestamos.Select(p =>
            {
                var monto     = p.monto;
                var comision  = Math.Round(monto * (porcentaje / 100m), 2);
                var fecha     = p.fecha_inicio.ToString("yyyy-MM-dd");
                var asesor    = p.cobrador_id.HasValue && cobradores.ContainsKey(p.cobrador_id.Value)
                                    ? cobradores[p.cobrador_id.Value]
                                    : "—";
                // MONEYPINE-FIX: usar NombreHelper para evitar duplicar apellido materno en clientes legacy
                var cliente   = p.Cliente != null
                                    ? NombreHelper.BuildNombreCliente(
                                        p.Cliente.Usuario?.nombre,
                                        p.Cliente.Usuario?.apellido,
                                        p.Cliente.apellido_materno,
                                        p.cliente_id)
                                    : $"Cliente #{p.cliente_id}";
                var estadoCom = p.estatus == EstatusPrestamo.LIQUIDADO ? "PAGADO" : "PENDIENTE";
                return new
                {
                    id          = $"C-{(idx++):D3}",
                    credito     = p.prestamo_id,
                    cliente_id  = p.cliente_id,
                    cliente,
                    asesor,
                    cobrador_id = p.cobrador_id,
                    fecha,
                    importe     = monto,
                    porcentaje,
                    comision,
                    estado      = estadoCom,
                };
            }).ToList();

            return Ok(result);
        }
    }
}