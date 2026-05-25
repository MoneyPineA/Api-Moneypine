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

            var result = await query
                .OrderByDescending(p => p.prestamo_id)
                .Select(p => new {
                    p.prestamo_id,
                    p.cliente_id,
                    numero_cliente = p.Cliente.clave_cliente,
                    nombre = (p.Cliente.Usuario != null
                        ? (p.Cliente.Usuario.nombre ?? "") + " " + (p.Cliente.Usuario.apellido ?? "")
                        : "Cliente #" + p.cliente_id).Trim(),
                    apellido_materno = p.Cliente.apellido_materno,
                    p.monto,
                    p.tasa_interes,
                    p.plazo_meses,
                    p.forma_pago,
                    p.estatus,
                    p.fecha_creacion,
                    p.destino,
                    tipo_solicitud = p.grupo_id.HasValue ? "PRÉSTAMO GRUPAL" : "PRÉSTAMO PERSONAL",
                    p.grupo_id,
                    grupo_nombre = p.Grupo != null ? p.Grupo.nombre : null
                })
                .ToListAsync();

            return Ok(result);
        }

        // =====================================================
        // PATCH: api/Prestamo/{id}/aprobar
        // Aprueba un préstamo PENDIENTE → ACTIVO
        // =====================================================
        [HttpPatch("{id}/aprobar")]
        public async Task<IActionResult> Aprobar(int id)
        {
            var prestamo = await _context.Prestamos.FindAsync(id);
            if (prestamo == null)
                return NotFound("Préstamo no encontrado");
            if (prestamo.estatus != EstatusPrestamo.PENDIENTE)
                return BadRequest("Solo se pueden aprobar préstamos en estado PENDIENTE");

            prestamo.estatus = EstatusPrestamo.ACTIVO;
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

            if (p == null)
                return NotFound("Préstamo no encontrado");

            // Otros miembros del mismo grupo (solo si es préstamo grupal)
            var miembros_grupo = p.grupo_id.HasValue
                ? await _context.Prestamos
                    .Where(x => x.grupo_id == p.grupo_id && x.prestamo_id != p.prestamo_id)
                    .Include(x => x.Cliente).ThenInclude(c => c.Usuario)
                    .Select(x => new {
                        x.prestamo_id,
                        nombre = x.Cliente != null && x.Cliente.Usuario != null
                            ? ((x.Cliente.Usuario.nombre ?? "") + " " +
                               (x.Cliente.Usuario.apellido ?? "") + " " +
                               (x.Cliente.apellido_materno ?? "")).Trim()
                            : "Cliente #" + x.cliente_id,
                    })
                    .ToListAsync<object>()
                : null;

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
                p.pago_mes,
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
                nombre_cliente  = p.Cliente != null && p.Cliente.Usuario != null
                    ? ((p.Cliente.Usuario.nombre ?? "") + " " +
                       (p.Cliente.Usuario.apellido ?? "") + " " +
                       (p.Cliente.apellido_materno ?? "")).Trim()
                    : "Cliente #" + p.cliente_id,
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

            decimal montoTotal =
                dto.monto + (dto.monto * dto.tasa_interes / 100);

            decimal pagoMes =
                Math.Round(montoTotal / dto.plazo_meses, 2);

            DateTime fechaInicio =
                fechaCreacion.AddMonths(1);

            DateTime fechaFin =
                fechaInicio.AddMonths(dto.plazo_meses - 1);

            decimal moraDiaria = dto.moratorio_por_dia.HasValue && dto.moratorio_por_dia.Value > 0
                ? dto.moratorio_por_dia.Value
                : Math.Round((pagoMes * 0.10m) / 30m, 2);

            var prestamo = new Prestamo
            {
                cliente_id = dto.cliente_id,
                monto = dto.monto,
                tasa_interes = dto.tasa_interes,
                plazo_meses = dto.plazo_meses,
                fecha_creacion = fechaCreacion,
                dias_gracia = dto.dias_gracia,

                monto_total = montoTotal,

                // SALDO INICIAL
                saldo_actual = montoTotal,

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

                estatus = EstatusPrestamo.PENDIENTE
            };

            _context.Prestamos.Add(prestamo);
            await _context.SaveChangesAsync();

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

            // Ruta vinculada → actualizar el Cliente asociado
            if (prestamo.Cliente != null && dto.ruta_vinculacion != null)
                prestamo.Cliente.ruta_vinculacion = dto.ruta_vinculacion;

            // ================================
            // RECALCULAR DATOS FINANCIEROS
            // ================================

            prestamo.monto_total =
                prestamo.monto + (prestamo.monto * prestamo.tasa_interes / 100);

            prestamo.pago_mes =
                Math.Round(prestamo.monto_total / prestamo.plazo_meses, 2);

            prestamo.saldo_actual = prestamo.monto_total;

            prestamo.fecha_inicio =
                prestamo.fecha_creacion.AddMonths(1);

            prestamo.fecha_fin =
                prestamo.fecha_inicio.AddMonths(prestamo.plazo_meses - 1);

            prestamo.fecha_proximo_pago =
                prestamo.fecha_inicio;

            prestamo.mora_diaria =
                Math.Round((prestamo.pago_mes * 0.10m) / 30m, 2);

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                1,
                "Préstamo actualizado",
                NotificationLevel.NEUTRAL
            );

            return NoContent();
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
                    // MONEYPINE-FIX: mora congelada — estadoPago CONGELADO para distinguir
                    //   de RETRASO en el contador de moratorios vigentes (BUG 1)
                    estadoPago       = "CONGELADO";
                    interesMoratorio = p.interes_moratorio;
                    diasMoratorio    = p.dias_moratorio;
                }
                else if (fechaVenc <= hoy) // MONEYPINE-FIX: <= incluye periodos que vencen hoy
                {
                    // MONEYPINE-FIX: vencido sin pagar — recalcular moratorio sobre capital insoluto
                    //   formula: capitalPendiente × (tasaAnualMoratorio/100/365) × diasMora
                    estadoPago    = "RETRASO";
                    diasMoratorio = (int)(hoy - fechaVenc).TotalDays;
                    interesMoratorio = diasMoratorio > 0 && prestamo.mora_diaria > 0 // MONEYPINE-FIX: usa mora_diaria directa (tasa_moratorio_anual = 0 en migración)
                        ? Math.Round(prestamo.mora_diaria * diasMoratorio, 2)
                        : 0;
                }
                else
                {
                    // Próximo período o futuro
                    estadoPago       = fechaVenc <= hoy.AddDays(freqDays) ? "PENDIENTE" : "INACTIVO";
                    interesMoratorio = 0;
                    diasMoratorio    = 0;
                }

                var interes       = p.interes_normal + p.interes_iva;
                var pagoProgramado = p.abono_capital + interes;

                return new {
                    periodo          = p.periodo,
                    fechaPago        = p.fecha_vencimiento.ToString("yyyy-MM-dd"),
                    saldoPendiente   = p.capital_pendiente + interes + interesMoratorio, // MONEYPINE-FIX: incluye interés + mora
                    capitalPendiente = p.capital_pendiente,
                    abonoCapital     = p.abono_capital,
                    interes,
                    interesIVA       = interes,
                    interesMoratorio,
                    diasMoratorio,
                    saldoFinal       = p.saldo_final,
                    pagoProgramado,
                    pagoTotal        = pagoProgramado + interesMoratorio,
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
                var cliente   = p.Cliente != null && p.Cliente.Usuario != null
                                    ? $"{p.Cliente.Usuario.nombre} {p.Cliente.Usuario.apellido} {p.Cliente.apellido_materno}".Trim()
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