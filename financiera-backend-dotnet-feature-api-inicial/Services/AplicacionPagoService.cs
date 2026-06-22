using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    /// <summary>
    /// Motor único de pagos. CalcularDistribucion es puro (no muta entidades, no escribe en DB).
    /// RecalcularEstadoPeriodo, RecalcularSaldo y RecalcularEstatus sí leen la DB y mutan entidades
    /// pasadas por referencia — usarlos solo desde el controller dentro de una transacción.
    /// </summary>
    public class AplicacionPagoService
    {
        private readonly AppDbContext _context;

        public AplicacionPagoService(AppDbContext context) { _context = context; }

        // ── Whitelist de tipos válidos ─────────────────────────────────────

        private static readonly HashSet<string> TiposValidos = new(StringComparer.Ordinal)
        {
            "parcialidad_mora", "parcialidad", "solo_mora",
            "solo_capital", "solo_interes", "pago_total"
        };

        public static bool TipoPagoValido(string? tipo) =>
            tipo == null || TiposValidos.Contains(tipo); // null se trata como "parcialidad"

        // ── DTOs de resultado (usados por controller y preview) ───────────

        public class DetallePerPeriodo
        {
            public int?    periodo_id       { get; set; }
            public int     periodo_num      { get; set; }
            public decimal capital_aplicado { get; set; }
            public decimal interes_aplicado { get; set; }
            public decimal iva_aplicado     { get; set; }
            public decimal mora_aplicada    { get; set; }
            public bool    periodo_cerrado  { get; set; }
            public int     nuevo_estado     { get; set; } = 3;
            public int     dias_moratorio   { get; set; }
            public decimal interes_moratorio { get; set; }
        }

        public class ResultadoDistribucion
        {
            public bool    ok           { get; set; } = true;
            public string? error        { get; set; }
            public decimal capital_total { get; set; }
            public decimal interes_total { get; set; }
            public decimal iva_total    { get; set; }
            public decimal mora_total   { get; set; }
            public decimal saldo_nuevo  { get; set; }
            public string  tipo_pago    { get; set; } = "";
            public List<DetallePerPeriodo> detalles { get; set; } = new();
        }

        // ── Struct interno: acumulado real desde pago_detalle por periodo ─

        private record AcumPd(decimal Cap, decimal Int, decimal Iva);

        // ─────────────────────────────────────────────────────────────────
        // CargarAcumPorPeriodo
        // Suma capital/interes/iva ya aplicados en pago_detalle de pagos
        // APLICADOS para el préstamo. Usado para calcular pendientes reales.
        // ─────────────────────────────────────────────────────────────────

        private async Task<Dictionary<int, AcumPd>> CargarAcumPorPeriodo(int prestamoId)
        {
            var rows = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == prestamoId && pd.periodo_id != null)
                .Join(_context.Pagos.Where(p => p.estatus == EstatusPago.APLICADO),
                      pd => pd.pago_id, p => p.pago_id, (pd, _) => pd)
                .GroupBy(pd => pd.periodo_id!.Value)
                .Select(g => new
                {
                    pid  = g.Key,
                    cap  = g.Sum(x => x.capital_aplicado),
                    int_ = g.Sum(x => x.interes_aplicado),
                    iva  = g.Sum(x => x.iva_aplicado),
                })
                .ToListAsync();

            return rows.ToDictionary(r => r.pid, r => new AcumPd(r.cap, r.int_, r.iva));
        }

        // ─────────────────────────────────────────────────────────────────
        // CalcularDistribucion  (PURO — no escribe en DB, no muta entidades)
        // ─────────────────────────────────────────────────────────────────

        public async Task<ResultadoDistribucion> CalcularDistribucion(PagoCreateDTO dto)
        {
            var res = new ResultadoDistribucion { tipo_pago = dto.tipo_pago };

            // null tipo_pago se interpreta como "parcialidad"
            if (dto.tipo_pago == null) dto.tipo_pago = "parcialidad";

            if (!TipoPagoValido(dto.tipo_pago))
                return Err(res, $"tipo_pago '{dto.tipo_pago}' no reconocido. " +
                                $"Valores válidos: {string.Join(", ", TiposValidos)}");

            if (dto.monto_pagado <= 0)
                return Err(res, "El monto debe ser mayor a 0");

            var prestamo = await _context.Prestamos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.prestamo_id == dto.prestamo_id);

            if (prestamo == null)
                return Err(res, "El préstamo no existe");
            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO)
                return Err(res, "El préstamo ya está liquidado");

            DateTime fechaPago = dto.fecha_pago.HasValue
                ? DateTime.SpecifyKind(dto.fecha_pago.Value.Date, DateTimeKind.Unspecified)
                : TimeHelper.GetMexicoTime();

            // Periodos pendientes y congelados
            var pendientes = await _context.PeriodosAmortizacion.AsNoTracking()
                .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 1)
                .OrderBy(pa => pa.periodo)
                .ToListAsync();

            var congelados = dto.tipo_pago == "solo_mora"
                ? await _context.PeriodosAmortizacion.AsNoTracking()
                    .Where(pa => pa.prestamo_id == dto.prestamo_id && pa.estado_pago == 5)
                    .OrderBy(pa => pa.periodo)
                    .ToListAsync()
                : new List<PeriodoAmortizacion>();

            // Acumulados reales desde pago_detalle (antes de este pago)
            var acum = await CargarAcumPorPeriodo(dto.prestamo_id);

            // Máximo permitido (con pendientes reales)
            decimal max = CalcMax(dto.tipo_pago, pendientes, congelados, prestamo, acum, fechaPago);

            if (max > 0 && dto.monto_pagado > max * 1.02m)
                return Err(res, $"El monto ({dto.monto_pagado:N2}) excede el adeudo ({max:N2}).");

            // Los pagos anteriores (incluso del mismo día) ya están descontados en acum
            // (CargarAcumPorPeriodo incluye todos los pagos APLICADO).
            // No se suma pagoAcumuladoDia para evitar duplicar la distribución.
            decimal pagoRestante = dto.monto_pagado;

            switch (dto.tipo_pago)
            {
                case "solo_mora":
                    AplicarSoloMora(res, pendientes, congelados, prestamo, fechaPago, ref pagoRestante);
                    res.saldo_nuevo = prestamo.saldo_actual; // mora no reduce capital
                    break;

                case "solo_capital":
                    AplicarSoloCapital(res, pendientes, prestamo, acum, dto.monto_pagado);
                    res.saldo_nuevo = Math.Max(0m, prestamo.saldo_actual - res.capital_total);
                    break;

                case "solo_interes":
                    AplicarSoloInteres(res, pendientes, acum, dto.monto_pagado);
                    res.saldo_nuevo = prestamo.saldo_actual; // interés no reduce capital
                    break;

                default: // parcialidad, parcialidad_mora, pago_total
                {
                    string? err = AplicarPorPeriodos(res, pendientes, prestamo, dto.tipo_pago,
                                                      fechaPago, dto.monto_pagado, acum, ref pagoRestante);
                    if (err != null) return Err(res, err);
                    res.saldo_nuevo = Math.Max(0m, prestamo.saldo_actual - res.capital_total);
                    break;
                }
            }

            return res;
        }

        // ─────────────────────────────────────────────────────────────────
        // CalcMax — máximo permitido usando pendientes reales desde pago_detalle
        // ─────────────────────────────────────────────────────────────────

        private static decimal CalcMax(string tipo,
            List<PeriodoAmortizacion> pendientes,
            List<PeriodoAmortizacion> congelados,
            Prestamo prestamo, Dictionary<int, AcumPd> acum, DateTime fechaPago)
        {
            AcumPd A(int id) => acum.GetValueOrDefault(id, new AcumPd(0, 0, 0));

            return tipo switch
            {
                "solo_mora" =>
                    congelados.Sum(p => Math.Max(0m, p.interes_moratorio - p.ahorro_por_pago))
                    + pendientes.Sum(p =>
                    {
                        int d = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                        decimal m = d > 0 ? Math.Round(prestamo.mora_diaria * d, 2) : 0m;
                        return Math.Max(0m, m - p.ahorro_por_pago);
                    }),

                "solo_capital" =>
                    pendientes.Sum(p => Math.Max(0m, p.abono_capital - A(p.periodo_id).Cap)),

                "solo_interes" =>
                    pendientes.Sum(p =>
                        Math.Max(0m, p.interes_normal - A(p.periodo_id).Int) +
                        Math.Max(0m, p.interes_iva   - A(p.periodo_id).Iva)),

                "parcialidad" =>
                    pendientes.Sum(p =>
                        Math.Max(0m, p.abono_capital  - A(p.periodo_id).Cap) +
                        Math.Max(0m, p.interes_normal - A(p.periodo_id).Int) +
                        Math.Max(0m, p.interes_iva    - A(p.periodo_id).Iva)),

                _ => // parcialidad_mora, pago_total
                    pendientes.Sum(p =>
                    {
                        var a = A(p.periodo_id);
                        int d = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                        decimal mora = d > 0 ? Math.Round(prestamo.mora_diaria * d, 2) : 0m;
                        return Math.Max(0m, p.abono_capital  - a.Cap) +
                               Math.Max(0m, p.interes_normal - a.Int) +
                               Math.Max(0m, p.interes_iva    - a.Iva) +
                               Math.Max(0m, mora - p.ahorro_por_pago);
                    }),
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // solo_mora — aplica mora a congelados primero, luego a pendientes
        // Nunca cierra periodos.
        // ─────────────────────────────────────────────────────────────────

        private void AplicarSoloMora(ResultadoDistribucion res,
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
                    periodo_cerrado = false,
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
                    periodo_cerrado = false,
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // solo_capital — aplica capital usando pendiente real.
        // NUNCA cierra periodos (falta interés/IVA).
        // ─────────────────────────────────────────────────────────────────

        private static void AplicarSoloCapital(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            Prestamo prestamo,
            Dictionary<int, AcumPd> acum,
            decimal monto)
        {
            decimal restante = monto;
            foreach (var p in pendientes)
            {
                if (restante <= 0) break;
                var a = acum.GetValueOrDefault(p.periodo_id, new AcumPd(0, 0, 0));
                decimal capPend = Math.Max(0m, p.abono_capital - a.Cap);
                if (capPend <= 0) continue;
                decimal capAplicar = Math.Min(restante, capPend);
                res.capital_total += capAplicar;
                restante          -= capAplicar;
                res.detalles.Add(new DetallePerPeriodo
                {
                    periodo_id       = p.periodo_id,
                    periodo_num      = p.periodo,
                    capital_aplicado = capAplicar,
                    periodo_cerrado  = false, // solo_capital NUNCA cierra
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // solo_interes — aplica interés+IVA usando pendiente real.
        // NUNCA cierra periodos (falta capital).
        // ─────────────────────────────────────────────────────────────────

        private static void AplicarSoloInteres(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            Dictionary<int, AcumPd> acum,
            decimal monto)
        {
            decimal restante = monto;
            foreach (var p in pendientes)
            {
                if (restante <= 0) break;
                var a = acum.GetValueOrDefault(p.periodo_id, new AcumPd(0, 0, 0));
                decimal intPend = Math.Max(0m, p.interes_normal - a.Int);
                decimal ivaPend = Math.Max(0m, p.interes_iva    - a.Iva);
                if (intPend + ivaPend <= 0) continue;
                decimal intA = Math.Min(restante, intPend); restante -= intA;
                decimal ivaA = Math.Min(restante, ivaPend); restante -= ivaA;
                res.interes_total += intA;
                res.iva_total     += ivaA;
                res.detalles.Add(new DetallePerPeriodo
                {
                    periodo_id       = p.periodo_id,
                    periodo_num      = p.periodo,
                    interes_aplicado = intA,
                    iva_aplicado     = ivaA,
                    periodo_cerrado  = false, // solo_interes NUNCA cierra
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // parcialidad / parcialidad_mora / pago_total
        // Descuenta pago_detalle existente antes de calcular el costo del periodo.
        // Un periodo cierra solo cuando cap+int+iva acumulados lo cubren todo.
        // ─────────────────────────────────────────────────────────────────

        private static string? AplicarPorPeriodos(ResultadoDistribucion res,
            List<PeriodoAmortizacion> pendientes,
            Prestamo prestamo, string tipoPago,
            DateTime fechaPago, decimal montoOriginal,
            Dictionary<int, AcumPd> acum, ref decimal pagoRestante)
        {
            foreach (var p in pendientes)
            {
                var a = acum.GetValueOrDefault(p.periodo_id, new AcumPd(0, 0, 0));

                // Pendiente real (restando lo ya pagado en pago_detalle)
                decimal capPend  = Math.Max(0m, p.abono_capital  - a.Cap);
                decimal intPend  = Math.Max(0m, p.interes_normal - a.Int);
                decimal ivaPend  = Math.Max(0m, p.interes_iva    - a.Iva);

                int d = Math.Max(0, (int)(fechaPago.Date - p.fecha_vencimiento.Date).TotalDays);
                decimal moraBruta = d > 0 ? Math.Round(prestamo.mora_diaria * d, 2) : 0m;
                decimal moraPend  = Math.Max(0m, moraBruta - p.ahorro_por_pago);

                // Costo del periodo para este tipo de pago
                decimal costo = tipoPago == "parcialidad"
                    ? capPend + intPend + ivaPend
                    : capPend + intPend + ivaPend + moraPend;

                if (costo <= 0.01m) continue; // ya cubierto — saltar

                if (pagoRestante >= costo - 0.05m)
                {
                    // Cobertura completa de este periodo
                    pagoRestante -= costo;
                    decimal moraCub = tipoPago == "parcialidad" ? 0m : moraPend;

                    // Determinar si cierra: cap+int+iva acumulados cubren el total del periodo
                    decimal capTotal = a.Cap + capPend;
                    decimal intTotal = a.Int + intPend;
                    decimal ivaTotal = a.Iva + ivaPend;
                    bool cerrado = capTotal >= p.abono_capital  - 0.01m
                                && intTotal >= p.interes_normal - 0.01m
                                && ivaTotal >= p.interes_iva    - 0.01m;

                    bool moraCubierta = tipoPago != "parcialidad"
                        && (p.ahorro_por_pago + moraCub) >= moraBruta - 0.01m;
                    int nuevoEstado = cerrado
                        ? (moraBruta <= 0.01m || moraCubierta ? 3 : 5)
                        : 1;

                    res.capital_total  += capPend;
                    res.interes_total  += intPend;
                    res.iva_total      += ivaPend;
                    res.mora_total     += moraCub;

                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id        = p.periodo_id,
                        periodo_num       = p.periodo,
                        capital_aplicado  = capPend,
                        interes_aplicado  = intPend,
                        iva_aplicado      = ivaPend,
                        mora_aplicada     = moraCub,
                        periodo_cerrado   = cerrado,
                        nuevo_estado      = nuevoEstado,
                        dias_moratorio    = d,
                        interes_moratorio = moraBruta,
                    });
                }
                else if (tipoPago == "parcialidad" && pagoRestante > 0.01m)
                {
                    // Pago parcial del primer periodo incompleto (solo para parcialidad)
                    // Orden: Capital → Interés → IVA
                    decimal sob  = pagoRestante;
                    decimal capA = Math.Min(sob, capPend); sob -= capA;
                    decimal intA = Math.Min(sob, intPend); sob -= intA;
                    decimal ivaA = Math.Min(sob, ivaPend);

                    // ¿El parcial completa la cobertura al combinar con acum previo?
                    decimal capTotal = a.Cap + capA;
                    decimal intTotal = a.Int + intA;
                    decimal ivaTotal = a.Iva + ivaA;
                    bool cerrado = capTotal >= p.abono_capital  - 0.01m
                                && intTotal >= p.interes_normal - 0.01m
                                && ivaTotal >= p.interes_iva    - 0.01m;
                    int nuevoEstado = cerrado ? (moraBruta > 0.01m ? 5 : 3) : 1;

                    res.capital_total  += capA;
                    res.interes_total  += intA;
                    res.iva_total      += ivaA;
                    pagoRestante        = 0m;

                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id       = p.periodo_id,
                        periodo_num      = p.periodo,
                        capital_aplicado = capA,
                        interes_aplicado = intA,
                        iva_aplicado     = ivaA,
                        periodo_cerrado  = cerrado,
                        nuevo_estado     = nuevoEstado,
                        dias_moratorio   = d,
                        interes_moratorio = moraBruta,
                    });
                    break;
                }
                else if (tipoPago != "parcialidad"
                      && pagoRestante >= capPend + intPend + ivaPend - 0.05m
                      && capPend + intPend + ivaPend > 0.01m)
                {
                    // Cubre cap+int+iva pero no la mora completa → período queda Congelado
                    decimal sob = pagoRestante;
                    decimal cA  = Math.Min(sob, capPend); sob -= cA;
                    decimal iA  = Math.Min(sob, intPend); sob -= iA;
                    decimal vA  = Math.Min(sob, ivaPend); sob -= vA;
                    decimal mA  = Math.Min(sob, moraPend); // mora residual si queda saldo
                    pagoRestante -= (cA + iA + vA + mA);

                    decimal capTotal = a.Cap + cA;
                    decimal intTotal = a.Int + iA;
                    decimal ivaTotal = a.Iva + vA;
                    bool cerrado = capTotal >= p.abono_capital  - 0.01m
                                && intTotal >= p.interes_normal - 0.01m
                                && ivaTotal >= p.interes_iva    - 0.01m;
                    bool moraCubierta = (p.ahorro_por_pago + mA) >= moraBruta - 0.01m;
                    int nuevoEstado = cerrado
                        ? (moraBruta <= 0.01m || moraCubierta ? 3 : 5)
                        : 1;

                    res.capital_total += cA; res.interes_total += iA;
                    res.iva_total     += vA; res.mora_total    += mA;
                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id        = p.periodo_id,
                        periodo_num       = p.periodo,
                        capital_aplicado  = cA,
                        interes_aplicado  = iA,
                        iva_aplicado      = vA,
                        mora_aplicada     = mA,
                        periodo_cerrado   = cerrado,
                        nuevo_estado      = nuevoEstado,
                        dias_moratorio    = d,
                        interes_moratorio = moraBruta,
                    });
                    // pago_total continúa a la siguiente parcialidad si queda saldo
                }
                else if (tipoPago != "parcialidad" && moraPend > 0.01m && pagoRestante <= moraPend + 0.05m)
                {
                    // Solo cubre mora del primer periodo — acumular sin cerrar
                    res.mora_total  += pagoRestante;
                    res.detalles.Add(new DetallePerPeriodo
                    {
                        periodo_id      = p.periodo_id,
                        periodo_num     = p.periodo,
                        mora_aplicada   = pagoRestante,
                        periodo_cerrado = false,
                    });
                    pagoRestante = 0m;
                    break;
                }
                else
                {
                    return $"El monto ({montoOriginal:N2}) no alcanza para cubrir el período " +
                           $"(${costo:N2}). Use tipo_pago=parcialidad para pagar sin mora.";
                }
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // RecalcularEstadoPeriodo
        // Lee pago_detalle de la DB (incluyendo los recién guardados) y
        // determina si el periodo debe cerrarse. Muta el objeto pa pasado.
        // Llamar DESPUÉS de guardar los nuevos pago_detalle en DB.
        // ─────────────────────────────────────────────────────────────────

        public async Task<int> RecalcularEstadoPeriodo(
            PeriodoAmortizacion pa, Prestamo prestamo, DateTime referencia)
        {
            var acum = await _context.PagoDetalles
                .Where(pd => pd.periodo_id == pa.periodo_id)
                .Join(_context.Pagos.Where(p => p.estatus == EstatusPago.APLICADO),
                      pd => pd.pago_id, p => p.pago_id, (pd, _) => pd)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    cap  = g.Sum(x => x.capital_aplicado),
                    int_ = g.Sum(x => x.interes_aplicado),
                    iva  = g.Sum(x => x.iva_aplicado),
                })
                .FirstOrDefaultAsync();

            decimal capAcum = acum?.cap  ?? 0;
            decimal intAcum = acum?.int_ ?? 0;
            decimal ivaAcum = acum?.iva  ?? 0;

            // Período vacío (sin capital, interés ni IVA): no marcar como PAGADO
            // automáticamente aunque acum=0 cumpla la condición 0 >= -0.01.
            bool periodoVacio = pa.abono_capital  <= 0.01m
                             && pa.interes_normal <= 0.01m
                             && pa.interes_iva    <= 0.01m;

            bool capOk = !periodoVacio && capAcum >= pa.abono_capital  - 0.01m;
            bool intOk = !periodoVacio && intAcum >= pa.interes_normal - 0.01m;
            bool ivaOk = !periodoVacio && ivaAcum >= pa.interes_iva    - 0.01m;

            int dias = Math.Max(0, (int)(referencia.Date - pa.fecha_vencimiento.Date).TotalDays);
            decimal moraBruta = dias > 0 && prestamo.mora_diaria > 0
                ? Math.Round(prestamo.mora_diaria * dias, 2) : 0m;

            if (periodoVacio || !capOk || !intOk || !ivaOk)
            {
                // Periodo no cubierto — mantener pendiente
                pa.estado_pago      = 1;
                pa.fecha_pagado     = null;
                pa.dias_moratorio   = dias;
                pa.interes_moratorio = moraBruta;
                return 1;
            }

            // Todos cubiertos — cerrar según mora
            bool moraCubierta = pa.ahorro_por_pago >= moraBruta - 0.01m;
            int nuevoEstado = moraCubierta ? 3 : 5;
            pa.estado_pago      = nuevoEstado;
            pa.fecha_pagado     ??= referencia; // preservar fecha original si ya existía
            pa.dias_moratorio   = dias;
            pa.interes_moratorio = moraBruta;
            return nuevoEstado;
        }

        // ─────────────────────────────────────────────────────────────────
        // RecalcularSaldo
        // saldo = monto - capital de pagos con detalle (pago_detalle)
        //               - capital de pagos legados sin detalle (pago.abono_capital)
        // ─────────────────────────────────────────────────────────────────

        public async Task<decimal> RecalcularSaldo(int prestamoId, decimal montoOriginal)
        {
            // IDs de pagos que tienen al menos un pago_detalle
            var pagosConDetalle = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == prestamoId)
                .Select(pd => pd.pago_id)
                .Distinct()
                .ToListAsync();

            // Capital de pagos CON detalle (usar pago_detalle.capital_aplicado)
            decimal capNuevo = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == prestamoId && pd.periodo_id != null)
                .Join(_context.Pagos.Where(p => p.estatus == EstatusPago.APLICADO),
                      pd => pd.pago_id, p => p.pago_id, (pd, _) => pd)
                .SumAsync(pd => (decimal?)pd.capital_aplicado) ?? 0m;

            // Capital de pagos legados SIN detalle (usar pago.abono_capital)
            decimal capLegado = await _context.Pagos
                .Where(p => p.prestamo_id == prestamoId
                         && p.estatus == EstatusPago.APLICADO
                         && !pagosConDetalle.Contains(p.pago_id))
                .SumAsync(p => (decimal?)p.abono_capital) ?? 0m;

            return Math.Max(0m, montoOriginal - capNuevo - capLegado);
        }

        // ─────────────────────────────────────────────────────────────────
        // RecalcularEstatus
        // LIQUIDADO si saldo <= 0; ATRASADO si hay periodo vencido pendiente; ACTIVO en otro caso.
        // ─────────────────────────────────────────────────────────────────

        public async Task<EstatusPrestamo> RecalcularEstatus(
            int prestamoId, decimal saldo, DateTime referencia)
        {
            if (saldo <= 0.01m) return EstatusPrestamo.LIQUIDADO;

            bool hayVencido = await _context.PeriodosAmortizacion
                .AnyAsync(pa => pa.prestamo_id == prestamoId
                             && pa.estado_pago == 1
                             && pa.fecha_vencimiento < referencia);

            return hayVencido ? EstatusPrestamo.ATRASADO : EstatusPrestamo.ACTIVO;
        }

        // ── Helper privado ────────────────────────────────────────────────

        private static ResultadoDistribucion Err(ResultadoDistribucion r, string msg)
        {
            r.ok = false; r.error = msg; return r;
        }
    }
}
