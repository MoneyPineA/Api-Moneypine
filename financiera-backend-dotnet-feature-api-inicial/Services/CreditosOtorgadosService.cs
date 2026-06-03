using ApiEjemplo.Data;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    public class CreditosOtorgadosService
    {
        private readonly AppDbContext _context;

        public CreditosOtorgadosService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<object> ObtenerCreditosOtorgadosAsync(
            int? año,
            Mes? mes,
            DateTime? fechaInicio,
            DateTime? fechaFin)
        {
            IQueryable<Prestamo> query = _context.Prestamos;

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

            var totalCreditos = await query.CountAsync();
            var montoTotalOtorgado = await query.SumAsync(p => p.monto);

            return new
            {
                CantidadCreditos = totalCreditos,
                MontoOtorgado = montoTotalOtorgado
            };
        }
    }
}