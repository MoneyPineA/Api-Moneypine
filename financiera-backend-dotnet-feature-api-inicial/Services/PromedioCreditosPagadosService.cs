using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    public class PromedioCreditosPagadosService
    {
        private readonly AppDbContext _context;

        public PromedioCreditosPagadosService(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // PROMEDIO POR AÑO / MES
        // ==========================================
        public async Task<decimal> CalcularPorAñoMesAsync(int año, Mes? mes)
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
        // PROMEDIO POR RANGO PERSONALIZADO
        // ==========================================
        public async Task<decimal> CalcularPorRangoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var prestamosLiquidados = _context.Prestamos
                .Where(p =>
                    p.estatus == EstatusPrestamo.LIQUIDADO &&
                    p.fecha_fin != null &&
                    p.fecha_fin >= fechaInicio &&
                    p.fecha_fin <= fechaFin
                );

            if (!await prestamosLiquidados.AnyAsync())
                return 0m;

            return await prestamosLiquidados.AverageAsync(p => p.monto);
        }
    }
}