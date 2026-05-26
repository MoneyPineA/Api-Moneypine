using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuroAutoReporteController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BuroAutoReporteController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/BuroAutoReporte — lista todos los clientes auto-reportados por mora >= 90 días
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var lista = await _db.BuroAutoReportes
                    .OrderByDescending(b => b.dias_mora)
                    .Select(b => new {
                        b.cliente_id,
                        b.prestamo_id,
                        b.dias_mora,
                        b.saldo_pendiente,
                        b.motivo,
                        fecha_reporte = b.fecha_reporte.ToString("yyyy-MM-dd HH:mm"),
                    })
                    .ToListAsync();

                return Ok(lista);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // DELETE /api/BuroAutoReporte/{clienteId} — quitar un cliente del auto-reporte (manual override)
        [HttpDelete("{clienteId:int}")]
        public async Task<IActionResult> Quitar(int clienteId)
        {
            try
            {
                var existing = await _db.BuroAutoReportes.FindAsync(clienteId);
                if (existing == null) return NotFound();
                _db.BuroAutoReportes.Remove(existing);
                await _db.SaveChangesAsync();
                return Ok(new { message = "Cliente removido del auto-reporte" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }
}
