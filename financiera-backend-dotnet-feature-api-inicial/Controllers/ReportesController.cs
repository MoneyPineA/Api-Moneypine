using ApiEjemplo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportesController : ControllerBase
    {
        private readonly InversionService _inversionService;
        private readonly CreditosOtorgadosService _creditosOtorgadosService;
        private readonly PromedioCreditosPagadosService _promedioService;
        private readonly TotalCreditosPagadosService _totalCreditosPagadosService;
        private readonly TotalGanadoPorPeriodoService _totalGanadoPorPeriodoService;

        public ReportesController(
            InversionService inversionService,
            CreditosOtorgadosService creditosOtorgadosService,
            PromedioCreditosPagadosService promedioService,
            TotalCreditosPagadosService totalCreditosPagadosService,
            TotalGanadoPorPeriodoService totalGanadoPorPeriodoService)
        {
            _inversionService = inversionService;
            _creditosOtorgadosService = creditosOtorgadosService;
            _promedioService = promedioService;
            _totalCreditosPagadosService = totalCreditosPagadosService;

            // ASIGNACIÓN
            _totalGanadoPorPeriodoService = totalGanadoPorPeriodoService;
        }

        // =========================
        // INVERSIÓN
        // =========================
        [HttpGet("inversion")]
        public async Task<IActionResult> ObtenerInversion(
            [FromQuery] int? año,
            [FromQuery] Mes? mes,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin)
        {
            if (fechaInicio.HasValue || fechaFin.HasValue)
            {
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                    return BadRequest("Debes enviar fechaInicio y fechaFin.");

                if (fechaInicio > fechaFin)
                    return BadRequest("fechaInicio no puede ser mayor a fechaFin.");
            }
            else if (!año.HasValue)
            {
                return BadRequest("Debes enviar un rango de fechas o un año.");
            }

            var inversion = await _inversionService
                .CalcularInversionAsync(año, mes, fechaInicio, fechaFin);

            return Ok(new
            {
                Año = año,
                Mes = mes?.ToString(),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                InversionActual = inversion,
                Moneda = "MXN",
                FechaConsulta = DateTime.Now
            });
        }


        // =========================
        // CRÉDITOS OTORGADOS
        // =========================
        [HttpGet("creditos-otorgados")]
        public async Task<IActionResult> ObtenerCreditosOtorgados(
            [FromQuery] int? año,
            [FromQuery] Mes? mes,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin)
        {
            if (fechaInicio.HasValue || fechaFin.HasValue)
            {
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                    return BadRequest("Debes enviar fechaInicio y fechaFin.");

                if (fechaInicio > fechaFin)
                    return BadRequest("fechaInicio no puede ser mayor a fechaFin.");
            }
            else if (!año.HasValue)
            {
                return BadRequest("Debes enviar un rango de fechas o un año.");
            }

            var resultado = await _creditosOtorgadosService
                .ObtenerCreditosOtorgadosAsync(año, mes, fechaInicio, fechaFin);

            return Ok(new
            {
                Año = año,
                Mes = mes?.ToString(),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                Resultado = resultado,
                Moneda = "MXN",
                FechaConsulta = DateTime.Now
            });
        }


        // =========================
        // PROMEDIO CRÉDITOS PAGADOS
        // =========================
        [HttpGet("promedio-creditos-pagados")]
        public async Task<IActionResult> ObtenerPromedioCreditosPagados(
            [FromQuery] int? año,
            [FromQuery] Mes? mes,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin)
        {
            decimal promedio;

            // CASO 1: Rango personalizado
            if (fechaInicio.HasValue || fechaFin.HasValue)
            {
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                    return BadRequest("Debes enviar fechaInicio y fechaFin.");

                if (fechaInicio > fechaFin)
                    return BadRequest("fechaInicio no puede ser mayor a fechaFin.");

                promedio = await _promedioService
                    .CalcularPorRangoAsync(fechaInicio.Value, fechaFin.Value);
            }
            // CASO 2: Año / Mes
            else if (año.HasValue)
            {
                promedio = await _promedioService
                    .CalcularPorAñoMesAsync(año.Value, mes);
            }
            else
            {
                return BadRequest("Debes enviar un rango de fechas o un año.");
            }

            return Ok(new
            {
                Año = año,
                Mes = mes?.ToString(),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                PromedioCreditosPagados = promedio,
                Moneda = "MXN",
                FechaConsulta = DateTime.Now
            });
        }


        // =========================
        // TOTAL CRÉDITOS PAGADOS
        // =========================
        [HttpGet("total-creditos-pagados")]
        public async Task<IActionResult> ObtenerTotalCreditosPagados(
            [FromQuery] int? año,
            [FromQuery] Mes? mes,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin)
        {
            object resultado;

            // CASO 1: Rango personalizado
            if (fechaInicio.HasValue || fechaFin.HasValue)
            {
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                    return BadRequest("Debes enviar fechaInicio y fechaFin.");

                if (fechaInicio > fechaFin)
                    return BadRequest("fechaInicio no puede ser mayor a fechaFin.");

                resultado = await _totalCreditosPagadosService
                    .CalcularPorRangoAsync(fechaInicio.Value, fechaFin.Value);
            }
            // CASO 2: Año / Mes
            else if (año.HasValue)
            {
                resultado = await _totalCreditosPagadosService
                    .CalcularPorAñoMesAsync(año.Value, mes);
            }
            else
            {
                return BadRequest("Debes enviar un rango de fechas o un año.");
            }

            return Ok(new
            {
                Resultado = resultado,
                Moneda = "MXN",
                FechaConsulta = DateTime.Now
            });
        }


        // =========================
        // TOTAL GANADO POR AÑO / MES
        // =========================
        [HttpGet("total-ganado")]
        public async Task<IActionResult> ObtenerTotalGanado(
            [FromQuery] int? año,
            [FromQuery] Mes? mes,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin)
        {
            decimal total;

            // CASO 1: Rango explícito
            if (fechaInicio.HasValue || fechaFin.HasValue)
            {
                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                    return BadRequest("Debes enviar fechaInicio y fechaFin.");

                if (fechaInicio > fechaFin)
                    return BadRequest("fechaInicio no puede ser mayor a fechaFin.");

                total = await _totalGanadoPorPeriodoService
                    .CalcularPorRangoAsync(fechaInicio, fechaFin);
            }
            // CASO 2: Año / Mes
            else if (año.HasValue)
            {
                total = await _totalGanadoPorPeriodoService
                    .CalcularPorañoMesAsync(año.Value, mes);
            }
            else
            {
                return BadRequest("Debes enviar un rango de fechas o un año.");
            }

            return Ok(new
            {
                Año = año,
                Mes = mes?.ToString(),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                TotalGanado = total,
                Moneda = "MXN",
                FechaConsulta = DateTime.Now
            });
        }

    }
}