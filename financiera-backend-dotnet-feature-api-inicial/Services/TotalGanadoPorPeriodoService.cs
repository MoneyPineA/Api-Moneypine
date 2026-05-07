using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    public class TotalGanadoPorPeriodoService
    {
        private readonly AppDbContext _context;

        public TotalGanadoPorPeriodoService(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // Calcula el total ganado (intereses + mora)
        // dentro de un rango de fechas.
        // =========================
        public async Task<decimal> CalcularPorRangoAsync(
            DateTime? fechaInicio,
            DateTime? fechaFin)
        {
            var query = _context.Pagos
                .Where(p => p.estatus == EstatusPago.APLICADO);

            if (fechaInicio.HasValue)
                query = query.Where(p => p.fecha_pago >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(p => p.fecha_pago <= fechaFin.Value);

            return await query.SumAsync(p =>
                p.interes_pagado + p.mora_pagada
            );
        }

        // =========================
        // Método auxiliar para calcular por año y mes exacto.
        // =========================
        public async Task<decimal> CalcularPorañoMesAsync(
            int año,
            Mes? mes)
        {
            DateTime fechaInicio;
            DateTime fechaFin;

            if (mes.HasValue)
            {
                fechaInicio = new DateTime(año, (int)mes.Value, 1);
                fechaFin = fechaInicio.AddMonths(1).AddDays(-1);
            }
            else
            {
                fechaInicio = new DateTime(año, 1, 1);
                fechaFin = new DateTime(año, 12, 31);
            }

            return await CalcularPorRangoAsync(fechaInicio, fechaFin);
        }
    }
}