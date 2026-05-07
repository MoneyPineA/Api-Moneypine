using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    public class TotalCreditosPagadosService
    {
        private readonly AppDbContext _context;

        public TotalCreditosPagadosService(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // POR AÑO / MES
        // ==========================================
        public async Task<object> CalcularPorAñoMesAsync(int año, Mes? mes)
        {
            DateTime fechaInicio;
            DateTime fechaFin;

            if (mes.HasValue)
            {
                int numeroMes = (int)mes.Value;

                fechaInicio = new DateTime(año, numeroMes, 1);
                fechaFin = fechaInicio.AddMonths(1).AddDays(-1);
            }
            else
            {
                fechaInicio = new DateTime(año, 1, 1);
                fechaFin = new DateTime(año, 12, 31);
            }

            return await CalcularPorRangoAsync(fechaInicio, fechaFin);
        }

        // ==========================================
        // POR RANGO PERSONALIZADO
        // ==========================================
        public async Task<object> CalcularPorRangoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var prestamosLiquidados = _context.Prestamos
                .Where(p => p.estatus == EstatusPrestamo.LIQUIDADO);

            var cantidadCreditos = await prestamosLiquidados.CountAsync();

            var totalRecuperado = await _context.Pagos
                .Where(pg =>
                    pg.estatus == EstatusPago.APLICADO &&
                    pg.fecha_pago >= fechaInicio &&
                    pg.fecha_pago <= fechaFin &&
                    prestamosLiquidados.Any(p => p.prestamo_id == pg.prestamo_id)
                )
                .SumAsync(pg => (decimal?)pg.monto_pagado) ?? 0m;

            return new
            {
                CantidadCreditosPagados = cantidadCreditos,
                TotalRecuperado = totalRecuperado,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            };
        }
    }
}