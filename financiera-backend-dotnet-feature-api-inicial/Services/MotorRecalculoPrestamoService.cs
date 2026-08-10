using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Helpers;
using ApiEjemplo.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    /// <summary>
    /// Motor determinista de reconstrucción de préstamos.
    /// Recalcula el estado completo del préstamo procesando todos sus pagos
    /// en orden cronológico sobre las parcialidades. No lee pago_detalle
    /// como fuente de verdad; lo regenera como caché al final.
    /// </summary>
    public class MotorRecalculoPrestamoService
    {
        private readonly AppDbContext _context;
        public MotorRecalculoPrestamoService(AppDbContext context) => _context = context;

        // ── Tipos que cubren mora ─────────────────────────────────────────
        private static readonly HashSet<string> TiposCubreMora =
            new(StringComparer.Ordinal) { "pago_total", "parcialidad_mora", "solo_mora" };

        // ── Tipo solo_mora no cubre cap/int/iva ───────────────────────────
        private static readonly HashSet<string> TiposSoloMora =
            new(StringComparer.Ordinal) { "solo_mora" };

        // ─────────────────────────────────────────────────────────────────
        // Reconstruir — punto de entrada
        // ─────────────────────────────────────────────────────────────────
        public async Task Reconstruir(int prestamoId)
        {
            var prestamo = await _context.Prestamos.FindAsync(prestamoId)
                ?? throw new InvalidOperationException($"Préstamo {prestamoId} no encontrado.");

            var periodos = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == prestamoId)
                .OrderBy(p => p.periodo)
                .ToListAsync();

            var pagos = await _context.Pagos
                .Where(p => p.prestamo_id == prestamoId && p.estatus == EstatusPago.APLICADO)
                .OrderBy(p => p.fecha_pago).ThenBy(p => p.pago_id)
                .ToListAsync();

            // ── 1. Resetear períodos ──────────────────────────────────────
            foreach (var p in periodos)
            {
                p.estado_pago       = 1;
                p.fecha_pagado      = null;
                p.ahorro_por_pago   = 0m;
                p.dias_moratorio    = 0;
                p.interes_moratorio = 0m;
            }

            // ── 2. Acumuladores en memoria ────────────────────────────────
            // capC, intC, ivaC, moraC: cuánto se ha cubierto de cada concepto por periodo
            var capC  = periodos.ToDictionary(p => p.periodo_id, _ => 0m);
            var intC  = periodos.ToDictionary(p => p.periodo_id, _ => 0m);
            var ivaC  = periodos.ToDictionary(p => p.periodo_id, _ => 0m);
            var moraC = periodos.ToDictionary(p => p.periodo_id, _ => 0m);
            // congelEn: fecha en que cap+int+iva quedaron cubiertos (mora se congela desde aquí)
            var congelEn = new Dictionary<int, DateTime>();

            // ── 3. Limpiar pago_detalle (se reconstruye como caché) ───────
            var viejos = await _context.PagoDetalles
                .Where(pd => pd.prestamo_id == prestamoId)
                .ToListAsync();
            _context.PagoDetalles.RemoveRange(viejos);

            // ── 4. Aplicar cada pago en orden cronológico ─────────────────
            decimal runningCap = 0m;

            foreach (var pago in pagos)
            {
                var fp    = DateTime.SpecifyKind(pago.fecha_pago.Date, DateTimeKind.Unspecified);
                string tp = pago.tipo_pago ?? "parcialidad"; // null → parcialidad
                decimal restante = pago.monto_pagado;

                decimal dCap = 0m, dInt = 0m, dIva = 0m, dMora = 0m;
                var dets = new List<PagoDetalle>();

                foreach (var per in periodos)
                {
                    if (restante <= 0.005m) break;
                    int pid = per.periodo_id;

                    // Mora: se congela en la fecha en que cap+int+iva quedaron cubiertos
                    DateTime refMora = congelEn.TryGetValue(pid, out var fe) ? fe : fp;
                    int diasMora = Math.Max(0, (int)(refMora.Date - per.fecha_vencimiento.Date).TotalDays);
                    // MONEYPINE-FIX: restar mora_condonada (durable) de la mora bruta recalculada —
                    // así una condonación previa no se vuelve a cobrar en pagos posteriores.
                    decimal moraBruta = diasMora > 0 && prestamo.mora_diaria > 0
                        ? Math.Max(0m, Math.Round(prestamo.mora_diaria * diasMora, 2) - per.mora_condonada) : 0m;

                    // Pendientes reales
                    decimal capPend  = Math.Max(0m, per.abono_capital  - capC[pid]);
                    decimal intPend  = Math.Max(0m, per.interes_normal - intC[pid]);
                    decimal ivaPend  = Math.Max(0m, per.interes_iva    - ivaC[pid]);
                    decimal moraPend = Math.Max(0m, moraBruta          - moraC[pid]);

                    decimal cA = 0m, iA = 0m, vA = 0m, mA = 0m;
                    bool debeContin; // si el período quedó completamente cubierto para este tipo

                    switch (tp)
                    {
                        case "solo_capital":
                        {
                            cA = Math.Min(restante, capPend);
                            debeContin = (capPend - cA) <= 0.005m;
                            break;
                        }
                        case "solo_interes":
                        {
                            decimal s = restante;
                            iA = Math.Min(s, intPend); s -= iA;
                            vA = Math.Min(s, ivaPend);
                            debeContin = (intPend - iA) <= 0.005m && (ivaPend - vA) <= 0.005m;
                            break;
                        }
                        case "solo_mora":
                        {
                            if (moraPend > 0.005m)
                            {
                                mA = Math.Min(restante, moraPend);
                                debeContin = (moraPend - mA) <= 0.005m;
                            }
                            else
                            {
                                debeContin = true; // sin mora aquí, pasar al siguiente
                            }
                            break;
                        }
                        case "parcialidad":
                        {
                            // Cubre Capital → Interés → IVA (no mora)
                            // Continúa al siguiente si cap+int+iva quedan cubiertos
                            decimal s = restante;
                            cA = Math.Min(s, capPend); s -= cA;
                            iA = Math.Min(s, intPend); s -= iA;
                            vA = Math.Min(s, ivaPend);
                            debeContin = (capPend - cA) <= 0.005m
                                      && (intPend - iA) <= 0.005m
                                      && (ivaPend - vA) <= 0.005m;
                            break;
                        }
                        default: // pago_total, parcialidad_mora
                        {
                            // Cubre Capital → Interés → IVA → Mora
                            // Continúa solo si todo queda cubierto
                            decimal s = restante;
                            cA = Math.Min(s, capPend); s -= cA;
                            iA = Math.Min(s, intPend); s -= iA;
                            vA = Math.Min(s, ivaPend); s -= vA;
                            mA = Math.Min(s, moraPend);
                            debeContin = (capPend - cA) <= 0.005m
                                      && (intPend - iA) <= 0.005m
                                      && (ivaPend - vA) <= 0.005m
                                      && (moraPend - mA) <= 0.005m;
                            break;
                        }
                    }

                    decimal totalAplicado = cA + iA + vA + mA;

                    // Nada aplicado → este período ya estaba cubierto, continuar al siguiente
                    if (totalAplicado <= 0.005m)
                    {
                        continue;
                    }

                    // Acumular
                    capC[pid]  += cA; intC[pid]  += iA;
                    ivaC[pid]  += vA; moraC[pid] += mA;
                    restante   -= totalAplicado;
                    dCap += cA; dInt += iA; dIva += vA; dMora += mA;

                    // ── Actualizar estado del período ─────────────────────
                    bool isVacio = per.abono_capital  <= 0.01m
                                && per.interes_normal <= 0.01m
                                && per.interes_iva    <= 0.01m;

                    if (!isVacio)
                    {
                        bool capOk = capC[pid] >= per.abono_capital  - 0.01m;
                        bool intOk = intC[pid] >= per.interes_normal - 0.01m;
                        bool ivaOk = ivaC[pid] >= per.interes_iva    - 0.01m;

                        if (capOk && intOk && ivaOk)
                        {
                            // Cap+Int+IVA cubiertos: congelar mora si es la primera vez
                            if (!congelEn.ContainsKey(pid))
                                congelEn[pid] = fp;

                            DateTime refM = congelEn[pid];
                            int dM = Math.Max(0, (int)(refM.Date - per.fecha_vencimiento.Date).TotalDays);
                            // MONEYPINE-FIX: idem — restar mora_condonada antes de congelar el valor final.
                            decimal moraFin = dM > 0 && prestamo.mora_diaria > 0
                                ? Math.Max(0m, Math.Round(prestamo.mora_diaria * dM, 2) - per.mora_condonada) : 0m;

                            bool moraCub = moraC[pid] >= moraFin - 0.01m;

                            per.estado_pago       = (moraCub || moraFin <= 0.01m) ? 3 : 5;
                            per.fecha_pagado      = per.fecha_pagado ?? fp;
                            per.ahorro_por_pago   = moraC[pid];
                            per.dias_moratorio    = dM;
                            per.interes_moratorio = moraFin;
                        }
                    }

                    // Registrar en pago_detalle (caché)
                    dets.Add(new PagoDetalle
                    {
                        pago_id          = pago.pago_id,
                        prestamo_id      = prestamoId,
                        periodo_id       = pid,
                        capital_aplicado = cA,
                        interes_aplicado = iA,
                        iva_aplicado     = vA,
                        mora_aplicada    = mA,
                        periodo_cerrado  = per.estado_pago == 3 || per.estado_pago == 5,
                        tipo_pago        = tp,
                        fecha_creacion   = TimeHelper.GetMexicoTime(),
                    });

                    if (!debeContin) break; // pago parcial: no continuar al siguiente período
                }

                // Actualizar distribución en el pago
                pago.abono_capital  = Math.Round(dCap,  2);
                pago.interes_pagado = Math.Round(dInt,  2);
                pago.interes_iva    = Math.Round(dIva,  2);
                pago.mora_pagada    = Math.Round(dMora, 2);
                runningCap         += pago.abono_capital;
                pago.saldo_restante = Math.Max(0m, prestamo.monto - runningCap);

                _context.Pagos.Update(pago);
                if (dets.Count > 0) _context.PagoDetalles.AddRange(dets);
            }

            // ── 5. Estado final de períodos aún pendientes ────────────────
            var hoy = TimeHelper.GetMexicoTime().Date;
            foreach (var per in periodos)
            {
                if (per.estado_pago == 1)
                {
                    int d = Math.Max(0, (int)(hoy - per.fecha_vencimiento.Date).TotalDays);
                    per.dias_moratorio    = d;
                    // MONEYPINE-FIX: idem — restar mora_condonada de la mora final de periodos
                    // aún pendientes, para que una condonación parcial no reaparezca aquí.
                    per.interes_moratorio = d > 0 && prestamo.mora_diaria > 0
                        ? Math.Max(0m, Math.Round(prestamo.mora_diaria * d, 2) - per.mora_condonada) : 0m;
                    per.ahorro_por_pago   = moraC.GetValueOrDefault(per.periodo_id, 0m);
                }
                _context.PeriodosAmortizacion.Update(per);
            }

            // ── 6. Estado del préstamo ────────────────────────────────────
            prestamo.saldo_actual = Math.Max(0m, prestamo.monto - runningCap);

            bool hayVencido      = periodos.Any(p => p.estado_pago == 1 && p.fecha_vencimiento.Date < hoy);
            bool hayMoraPendiente = periodos.Any(p => p.estado_pago == 5);

            if (prestamo.saldo_actual <= 0.01m && !hayMoraPendiente)
            {
                prestamo.estatus   = EstatusPrestamo.LIQUIDADO;
                prestamo.fecha_fin ??= DateTime.SpecifyKind(hoy, DateTimeKind.Unspecified);
            }
            else
            {
                prestamo.estatus   = (hayVencido || hayMoraPendiente)
                                     ? EstatusPrestamo.ATRASADO
                                     : EstatusPrestamo.ACTIVO;
                prestamo.fecha_fin = null;
            }

            var sigPend = periodos
                .Where(p => p.estado_pago == 1)
                .OrderBy(p => p.periodo)
                .Select(p => p.fecha_vencimiento)
                .FirstOrDefault();
            if (sigPend != default)
                prestamo.fecha_proximo_pago = sigPend;

            _context.Prestamos.Update(prestamo);
            await _context.SaveChangesAsync();
        }
    }
}
