using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    public class InversionService
    {
        private readonly AppDbContext _context;

        public InversionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalcularInversionAsync(
            int? año,
            Mes? mes,
            DateTime? fechaInicio,
            DateTime? fechaFin)
        {
            IQueryable<Prestamo> query = _context.Prestamos
                .Where(p =>
                    p.estatus == EstatusPrestamo.ACTIVO ||
                    p.estatus == EstatusPrestamo.ATRASADO
                );

            // =============================
            // CASO 1: Rango personalizado
            // =============================
            if (fechaInicio.HasValue && fechaFin.HasValue)
            {
                query = query.Where(p =>
                    p.fecha_creacion >= fechaInicio.Value &&
                    p.fecha_creacion <= fechaFin.Value);
            }
            // =============================
            // CASO 2: Año / Mes
            // =============================
            else if (año.HasValue)
            {
                if (mes.HasValue)
                {
                    query = query.Where(p =>
                        p.fecha_creacion.Year == año.Value &&
                        p.fecha_creacion.Month == (int)mes.Value);
                }
                else
                {
                    query = query.Where(p =>
                        p.fecha_creacion.Year == año.Value);
                }
            }
            else
            {
                throw new ArgumentException("Debes enviar un rango de fechas o un año.");
            }

            var inversion = await query
                .Select(p => (decimal?)p.saldo_actual)
                .SumAsync() ?? 0;

            return inversion;
        }
    }
}