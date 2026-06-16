using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/Cobranza")]
    public class CobranzaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CobranzaController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // GET: api/Cobranza/diaria
        // Parámetros: fechaInicio, fechaFin, formaPago,
        //             estatusCredito, estadoPago, cobradorId
        // =====================================================
        [HttpGet("diaria")]
        public async Task<IActionResult> GetCobranzaDiaria(
            DateTime? fechaInicio    = null,
            DateTime? fechaFin       = null,
            string?   tipoProducto   = null,
            string?   formaPago      = null,
            string?   estatusCredito = null,
            string?   estadoPago     = null,
            int?      cobradorId     = null,
            string?   ruta           = null)
        {
            // Frontend envía sólo YYYY-MM-DD (sin hora ni zona), sin conversión UTC
            var desde = fechaInicio.HasValue
                ? fechaInicio.Value.Date
                : TimeHelper.GetMexicoTime().Date;

            var hasta = fechaFin.HasValue
                ? fechaFin.Value.Date.AddDays(1).AddTicks(-1)   // fin del día seleccionado
                : desde.AddDays(1).AddTicks(-1);

            // ── Base query ──────────────────────────────────────
            var query = _context.Prestamos
                .Include(p => p.Cliente)
                    .ThenInclude(c => c.Usuario)
                .AsQueryable();

            // Estatus crédito
            if (!string.IsNullOrEmpty(estatusCredito) && estatusCredito != "Todos"
                && Enum.TryParse<EstatusPrestamo>(estatusCredito, true, out var es))
            {
                query = query.Where(p => p.estatus == es);
            }
            else
            {
                query = query.Where(p =>
                    (p.estatus == EstatusPrestamo.ACTIVO ||
                     p.estatus == EstatusPrestamo.ATRASADO) &&
                    p.estatus != EstatusPrestamo.LIQUIDADO);
            }

            // Cobrador
            if (cobradorId.HasValue)
                query = query.Where(p => p.cobrador_id == cobradorId.Value);

            // Ruta
            if (!string.IsNullOrEmpty(ruta) && ruta != "Todos")
            {
                var rutaNormalizada = NormalizeRoute(ruta);
                query = query.Where(p => p.Cliente.ruta_vinculacion != null && p.Cliente.ruta_vinculacion.Trim().ToUpper() == rutaNormalizada);
            }

            // Tipo de producto
            if (!string.IsNullOrEmpty(tipoProducto) && tipoProducto != "Todos")
            {
                var tipoNormalizado = tipoProducto.Trim().ToUpperInvariant();

                if (tipoNormalizado == "PRESTAMO GRUPAL")
                    query = query.Where(p => p.grupo_id.HasValue);
                else if (tipoNormalizado == "PRESTAMO PERSONAL")
                    query = query.Where(p => !p.grupo_id.HasValue);
            }

            // Forma de pago
            if (!string.IsNullOrEmpty(formaPago) && formaPago != "Todos"
                && Enum.TryParse<FormasPago>(formaPago, true, out var fp))
            {
                query = query.Where(p => p.forma_pago == fp);
            }

            // Todos los estatus (ACTIVO/ATRASADO): solo si fecha_proximo_pago cae en el rango.
            // La cartera vencida acumulada sin fecha en el rango pertenece a otro reporte.
            query = query.Where(p =>
                p.fecha_proximo_pago.HasValue &&
                p.fecha_proximo_pago.Value.Date >= desde.Date &&
                p.fecha_proximo_pago.Value.Date <= hasta.Date);

            var prestamos = await query.ToListAsync();

            // ── Pagos de cada préstamo ───────────────────────────
            var prestamoIds = prestamos.Select(p => p.prestamo_id).ToList();
            var pagosAll = await _context.Pagos
                .Where(p => prestamoIds.Contains(p.prestamo_id))
                .OrderByDescending(p => p.fecha_pago)
                .ToListAsync();

            // ── Filtro estadoPago ────────────────────────────────
            if (!string.IsNullOrWhiteSpace(estadoPago) && estadoPago != "Todos")
            {
                var pagosEnRangoIds = pagosAll
                    .Where(p => p.fecha_pago >= desde && p.fecha_pago <= hasta)
                    .Select(p => p.prestamo_id)
                    .Distinct()
                    .ToHashSet();

                var pagosHistoricosIds = pagosAll
                    .Select(p => p.prestamo_id)
                    .Distinct()
                    .ToHashSet();

                prestamos = estadoPago switch
                {
                    "NoPagado" => prestamos.Where(p => !pagosEnRangoIds.Contains(p.prestamo_id)).ToList(),
                    "PagadoEnRango" => prestamos.Where(p => pagosEnRangoIds.Contains(p.prestamo_id)).ToList(),
                    "ConPagos" => prestamos.Where(p => pagosHistoricosIds.Contains(p.prestamo_id)).ToList(),
                    "SinPagos" => prestamos.Where(p => !pagosHistoricosIds.Contains(p.prestamo_id)).ToList(),
                    _ => prestamos
                };

                prestamoIds = prestamos.Select(p => p.prestamo_id).ToList();
                pagosAll = pagosAll.Where(p => prestamoIds.Contains(p.prestamo_id)).ToList();
            }

            // Pagos en el rango para el resumen financiero
            var pagosEnRango = await _context.Pagos
                .Where(p => p.fecha_pago >= desde && p.fecha_pago <= hasta)
                .ToListAsync();

            decimal totalPendiente = prestamos.Sum(p => p.pago_mes);

            // ── Listado de cobros ────────────────────────────────
            var listado = prestamos.Select(p =>
            {
                var pagosP   = pagosAll.Where(pg => pg.prestamo_id == p.prestamo_id).ToList();
                var ultimoPago = pagosP.FirstOrDefault();
                return new
                {
                    idCredito        = p.prestamo_id,
                    clienteId        = p.cliente_id,
                    cliente          = $"{p.Cliente.Usuario?.nombre} {p.Cliente.Usuario?.apellido}".Trim(),
                    tipoProducto     = p.grupo_id.HasValue ? "PRESTAMO GRUPAL" : "PRESTAMO PERSONAL",
                    ruta             = p.Cliente.ruta_vinculacion ?? "—",
                    colonia          = p.Cliente.colonia            ?? "—",
                    fechaProximoPago = p.fecha_proximo_pago,
                    fechaUltimoPago  = ultimoPago?.fecha_pago,
                    cantidadPagos    = pagosP.Count,
                    montoCuota       = p.pago_mes,
                    formaPago        = p.forma_pago.ToString(),
                    estatus          = p.estatus.ToString(),
                    cobradorId       = p.cobrador_id,
                    latitud          = p.Cliente.latitud,
                    longitud         = p.Cliente.longitud
                };
            }).ToList();

            // ── Gráfico: cuotas agrupadas por fecha ─────────────
            var graficoAbonos = prestamos
                .Where(p => p.fecha_proximo_pago.HasValue)
                .GroupBy(p => p.fecha_proximo_pago!.Value.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    fecha = g.Key.ToString("yyyy-MM-dd"),
                    total = g.Sum(p => p.pago_mes)
                }).ToList();

            return Ok(new
            {
                resumen = new
                {
                    montoPendienteHoy = totalPendiente,
                    cantidadAbonos    = listado.Count,
                    totalAbonado      = pagosEnRango.Sum(p =>
                        p.monto_pagado + p.interes_pagado + p.mora_pagada)
                },
                graficoAbonos   = graficoAbonos,
                graficoProgreso = new
                {
                    capitalActual = pagosEnRango.Sum(p => p.monto_pagado),
                    interesActual = pagosEnRango.Sum(p => p.interes_pagado),
                    totalCartera  = pagosEnRango.Sum(p => p.monto_pagado + p.interes_pagado)
                },
                listadoCobros = listado
            });
        }

        // =====================================================
        // GET: api/Cobranza/rutas
        // Lista de rutas distintas registradas en clientes
        // =====================================================
        [HttpGet("rutas")]
        public async Task<IActionResult> GetRutas()
        {
            var rutasCatalogo = await _context.Rutas
                .OrderBy(r => r.nombre)
                .Select(r => r.nombre.Trim().ToUpper())
                .Distinct()
                .ToListAsync();

            if (rutasCatalogo.Count > 0)
                return Ok(rutasCatalogo);

            var rutasClientes = await _context.Clientes
                .Where(c => c.ruta_vinculacion != null && c.ruta_vinculacion != "")
                .Select(c => c.ruta_vinculacion!)
                .ToListAsync();

            var rutasFallback = rutasClientes
                .Select(NormalizeRoute)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            return Ok(rutasFallback);
        }

        // =====================================================
        // GET: api/Cobranza/ejecutivos
        // Lista de usuarios que son cobradores / admins
        // =====================================================
        [HttpGet("ejecutivos")]
        public async Task<IActionResult> GetEjecutivos()
        {
            var ejecutivos = await _context.Usuarios
                .Where(u => u.rol != RolUsuario.CLIENTE)
                .OrderBy(u => u.nombre)
                .Select(u => new
                {
                    id     = u.usuario_id,
                    nombre = $"{u.nombre} {u.apellido}",
                    rol    = u.rol.ToString()
                })
                .ToListAsync();

            return Ok(ejecutivos);
        }

        private static string NormalizeRoute(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            return normalized.ToUpperInvariant();
        }
    }
}
