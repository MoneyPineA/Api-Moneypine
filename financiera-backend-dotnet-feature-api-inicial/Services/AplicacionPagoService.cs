using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    /// <summary>
    /// Motor único para preview y aplicación de los 6 tipos de pago.
    /// CalcularDistribucion es puro (no muta entidades, no escribe en DB).
    /// El controller POST usa el resultado para aplicar los cambios.
    /// </summary>
    public class AplicacionPagoService
    {
        private readonly AppDbContext _context;
        public AplicacionPagoService(AppDbContext context) { _context = context; }

        // ── DTOs de resultado ─────────────────────────────────────────────

        public class DetallePerPeriodo
        {
            public int     periodo_id      { get; set; }
            public int     periodo_num     { get; set; }
            public decimal capital_aplicado { get; set; }
            public decimal interes_aplicado { get; set; }
            public decimal iva_aplicado     { get; set; }
            public decimal mora_aplicada    { get; set; }
            public bool    periodo_cerrado  { get; set; }
            // Para periodos cerrados: nuevo estado_pago (2=legacy,3=normal,5=congelado)
            public int     nuevo_estado     { get; set; } = 3;
            public int     dias_moratorio   { get; set; }
            public decimal interes_moratorio { get; set; }
            // Para periodos con ahorro parcial: cuánto sumar a ahorro_por_pago
            public decimal delta_ahorro     { get; set; }
        }

        public class ResultadoDistribucion
        {
            public bool    ok            { get; set; } = true;
            public string? error         { get; set; }

            public decimal capital_total  { get; set; }
            public decimal interes_total  { get; set; }
            public decimal iva_total      { get; set; }
            public decimal mora_total     { get; set; }
            public decimal saldo_nuevo    { get; set; }
            public string  tipo_pago      { get; set; } = "";

            public List<DetallePerPeriodo> detalles { get; set; } = new();
        }

        // ── Motor principal (puro — no escribe en DB, no muta entidades) ──

        public async Task<ResultadoDistribucion> CalcularDistribucion(PagoCreateDTO dto)
        {
            var res = new ResultadoDistribucion { tipo_pago = dto.tipo_pago };

            var prestamo = await _context.Prestamos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.prestamo_id == dto.prestamo_id);

            if (prestamo == null)          return Err(res, "El préstamo no existe");
            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                                           return Err(res, "El préstamo ya está liquidado");
            if (dto.monto_pagado <= 0)     return Err(res, "El monto debe ser mayor a 0");

            DateTime fechaPago = dto.fecha_pago.HasValue
                ? DateTime.SpecifyKind(dto.fecha_pago.Value.Date, DateTimeKind.Unspecified)
                : TimeHelper.GetMexicoTime();

            var pendientes = await _context.PeriodosAmortizacion
                .AsNoTracking()
                .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .ToListAsync();

            decimal interesesPend = pendientes.Any()
                ? pendientes.Sum(p => p.interes_normal + p.interes_iva)
                : (prestamo.monto > 0
                    ? Math.Round((prestamo.monto_total - prestamo.monto) * (prestamo.saldo_actual / prestamo.monto), 2)
                    : 0);

            var primerPend = pendientes.FirstOrDefault();
            DateTime fechaRefMora = primerPend?.fecha_vencimiento ?? prestamo.fecha_proximo_pago ?? fechaPago;
            decimal moraRef = 0;
            if (fechaPago.Date > fechaRefMora.Date.AddDays(prestamo.dias_gracia))
                moraRef = (fechaPago.Date - fechaRefMora.Date).Days * prestamo.mora_diaria;

            var congelados = (dto.tipo_pago == "solo_mora")
                ? await _context.PeriodosAmortizacion
                    .AsNoTracking()
                    .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 5)
                    .OrderBy(pa => pa.periodo)
                    .ToListAsync()
                : new List<PeriodoAmortizacion>();

            // Acumulado del mismo día para parcialidad_mora / parcialidad / pago_total
            var diaInicio = fechaPago.Date;
            decimal pagoAcumulado = await _context.Pagos
                .Where(p => p.prestamo_id == dto.prestamo_id
                         && p.fecha_pago >= diaInicio
                         && p.fecha_pago < diaInicio.AddDays(1)
                         && p.estatus == EstatusPago.APLICADO)
                .SumAsync(p => (decimal?)(p.monto_pagado - p.interes_pagado - p.mora_pagada - p.abono_capital)) ?? 0m;

            // maxPermitido
            decimal max = dto.tipo_pago switch
            {
                "solo_mora" =>
                    congelados.Sum(p => Math.Max(0m, p.interes_moratorio - p.ahorro_por_pago))
                    + pendientes.Sum(p => {
                        int d = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                        decimal m = d > 0 ? Math.Round(prestamo.mora_diaria * d, 2) : 0m;
                        return Math.Max(0m, m - p.ahorro_por_pago);
                    }),
                "solo_capital" =>
                    prestamo.saldo_actual,
                "solo_interes" =>
                    interesesPend,
                "parcialidad" =>
                    pendientes.Any()
                        ? pendientes.Sum(p => p.abono_capital + p.interes_normal + p.interes_iva)
                        : prestamo.saldo_actual + interesesPend,
                _ => // parcialidad_mora, pago_total
                    prestamo.saldo_actual + moraRef + interesesPend,
            };

            if (max > 0 && dto.monto_pagado > max * 1.02m)
                return Err(res, $"El monto ({dto.monto_pagado:N2}) excede el adeudo total ({max:N2}). " +
                                $"Saldo: ${prestamo.saldo_actual:N2}, Intereses: ${interesesPend:N2}, Mora: ${moraRef:N2}");

            decimal pagoRestante = dto.monto_pagado + pagoAcumulado;

            switch (dto.tipo_pago)
            {
                case "solo_mora":
                    SoloMora(res, pendientes, congelados, prestamo, fechaPago, ref pagoRestante);
                    res.saldo_nuevo = prestamo.saldo_actual;
                    break;

                case "solo_capital":
                    SoloCapital(res, pendientes, prestamo, dto.monto_pagado);
                    break;

                case "solo_interes":
                    SoloInteres(res, pendientes, prestamo, dto.monto_pagado);
                    break;

                default: // parcialidad, parcialidad_mora, pago_total
                    string? err = PorPeriodos(res, pendientes, prestamo, dto.tipo_pago, fechaPago, dto.monto_pagado, ref pagoRestante);
                    if (err != null) return Err(res, err);
                    int nCerrados = res.detalles.Count(d => d.periodo_cerrado);
                    res.saldo_nuevo = pendientes.Skip(nCerrados).Sum(p => p.abono_capital);
                    break;
            }

            return res;
        }

        // ── solo_mora ─────────────────────────────────────────────────────

        private void SoloMora(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            List<PeriodoAmortizacion> congelados,
            Prestamo prestamo, DateTime fechaPago, ref decimal pagoRestante)
        {
            foreach (var p in congelados)
            {
                if (pagoRestante <= 0) break;
                decimal moraRest = Math.Max(0m, p.interes_moratorio - p.ahorro_por_pago);
                if (moraRest <= 0) continue;
                decimal aplicar = pagoRestante >= moraRest - 0.05m ? moraRest : pagoRestante;
                res.mora_total += aplicar;
                pagoRestante   -= aplicar;
                res.detalles.Add(new DetallePerPeriodo
                {
                    periodo_id    = p.periodo_id,
                    periodo_num   = p.periodo,
                    mora_aplicada = aplicar,
                    delta_ahorro  = aplicar,
                });
            }
            foreach (var p in pendientes)
            {
                if (pagoRestante <= 0) break;
                int d = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                decimal mora = d > 0 ? Math.Round(prestamo.mora_diaria * d, 2) : 0m;
                decimal moraRest = Math.Max(0m, mora - p.ahorro_por_pago);
                if (moraRest <= 0) continue;
                decimal aplicar = pagoRestante >= moraRest - 0.05m ? moraRest : pagoRestante;
                res.mora_total += aplicar;
                pagoRestante   -= aplicar;
                res.detalles.Add(new DetallePerPeriodo
                {
                    periodo_id    = p.periodo_id,
                    periodo_num   = p.periodo,
                    mora_aplicada = aplicar,
                    delta_ahorro  = aplicar,
                });
            }
        }

        // ── solo_capital ─────────────────────────────────────────────────

        private void SoloCapital(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            Prestamo prestamo, decimal monto)
        {
            decimal restante = monto;
            int cerrados = 0;
            decimal capParcial = 0;

            foreach (var p in pendientes)
            {
                if (restante <= 0) break;
                decimal capAplicar = Math.Min(restante, p.abono_capital);
                res.capital_total += capAplicar;
                restante          -= capAplicar;
                bool cerrado = capAplicar >= p.abono_capital - 0.01m;
                if (cerrado) cerrados++;
                else capParcial = capAplicar;
                res.detalles.Add(new DetallePerPeriodo
                {
                    periodo_id       = p.periodo_id,
                    periodo_num      = p.periodo,
                    capital_aplicado = capAplicar,
                    periodo_cerrado  = cerrado,
                    nuevo_estado     = 3,
                });
            }
            decimal saldoBase = pendientes.Skip(cerrados).Sum(p => p.abono_capital);
            res.saldo_nuevo = Math.Max(0, saldoBase - capParcial);
        }

        // ── solo_interes ─────────────────────────────────────────────────

        private void SoloInteres(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            Prestamo prestamo, decimal monto)
        {
            decimal restante = monto;
            foreach (var p in pendientes)
            {
                if (restante <= 0) break;
                decimal intAplicar = Math.Min(restante, p.interes_normal);
                restante           -= intAplicar;
                decimal ivaAplicar = Math.Min(restante, p.interes_iva);
                restante           -= ivaAplicar;
                res.interes_total  += intAplicar;
                res.iva_total      += ivaAplicar;
                res.detalles.Add(new DetallePerPeriodo
                {
                    periodo_id       = p.periodo_id,
                    periodo_num      = p.periodo,
                    interes_aplicado = intAplicar,
                    iva_aplicado     = ivaAplicar,
                });
            }
            res.saldo_nuevo = prestamo.saldo_actual;
        }

        // ── parcialidad / parcialidad_mora / pago_total ───────────────────

        private string? PorPeriodos(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            Prestamo prestamo, string tipoPago,
            DateTime fechaPago, decimal montoOriginal, ref decimal pagoRestante)
        {
            foreach (var p in pendientes)
            {
                int     diasMora    = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                decimal moraPeriodo = diasMora > 0 ? Math.Round(prestamo.mora_diaria * diasMora, 2) : 0m;
                decimal moraEfec    = Math.Max(0m, moraPeriodo - p.ahorro_por_pago);

                decimal costo = tipoPago == "parcialidad"
                    ? p.abono_capital + p.interes_normal + p.interes_iva
                    : p.abono_capital + p.interes_normal + p.interes_iva + moraEfec;

                if (pagoRestante >= costo - 0.05m)
                {
                    pagoRestante     -= costo;
                    decimal moraCub   = tipoPago == "parcialidad" ? 0m : moraEfec;
                    res.capital_total += p.abono_capital;
                    res.interes_total += p.interes_normal;
                    res.iva_total     += p.interes_iva;
                    res.mora_total    += moraCub;

                    bool teniaMora = moraEfec > 0;
                    int nuevoEstado = (tipoPago == "parcialidad" && teniaMora) ? 5 : 3;

                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id        = p.periodo_id,
                        periodo_num       = p.periodo,
                        capital_aplicado  = p.abono_capital,
                        interes_aplicado  = p.interes_normal,
                        iva_aplicado      = p.interes_iva,
                        mora_aplicada     = moraCub,
                        periodo_cerrado   = true,
                        nuevo_estado      = nuevoEstado,
                        dias_moratorio    = diasMora,
                        interes_moratorio = moraEfec,
                    });
                }
                else if (tipoPago == "parcialidad" && pagoRestante > 0)
                {
                    // Pago parcial del primer periodo incompleto
                    decimal sob  = pagoRestante;
                    decimal intA = Math.Min(sob, p.interes_normal); sob -= intA;
                    decimal ivaA = Math.Min(sob, p.interes_iva);    sob -= ivaA;
                    decimal capA = sob;
                    res.interes_total += intA;
                    res.iva_total     += ivaA;
                    res.capital_total += capA;
                    pagoRestante       = 0m;
                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id       = p.periodo_id,
                        periodo_num      = p.periodo,
                        capital_aplicado = capA,
                        interes_aplicado = intA,
                        iva_aplicado     = ivaA,
                        periodo_cerrado  = false,
                    });
                    break;
                }
                else if (tipoPago != "parcialidad" && moraEfec > 0 && pagoRestante <= moraEfec + 0.05m)
                {
                    // Solo mora del primer periodo — acumular sin cerrar
                    decimal moraAplicar = pagoRestante;
                    res.mora_total  += moraAplicar;
                    pagoRestante     = 0m;
                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id    = p.periodo_id,
                        periodo_num   = p.periodo,
                        mora_aplicada = moraAplicar,
                        delta_ahorro  = moraAplicar,
                    });
                    break;
                }
                else
                {
                    return $"El monto ({montoOriginal:N2}) no alcanza para cerrar el período completo " +
                           $"(${costo:N2}). Para pagar solo mora use tipo_pago=solo_mora.";
                }
            }
            return null;
        }

        private static ResultadoDistribucion Err(ResultadoDistribucion r, string msg)
        {
            r.ok = false; r.error = msg; return r;
        }
    }
}
