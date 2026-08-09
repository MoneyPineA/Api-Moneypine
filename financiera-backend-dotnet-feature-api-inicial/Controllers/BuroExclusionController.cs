using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BuroExclusionController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BuroExclusionController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/BuroExclusion — lista de todos los cliente_id excluidos del buró
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var exclusiones = await _db.BuroExclusiones
                    .OrderByDescending(e => e.fecha)
                    .ToListAsync();
                return Ok(exclusiones);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // POST /api/BuroExclusion — excluir cliente del buró (quitar)
        [HttpPost]
        public async Task<IActionResult> Excluir([FromBody] BuroExclusion dto)
        {
            try
            {
                var existing = await _db.BuroExclusiones.FindAsync(dto.cliente_id);
                if (existing != null)
                {
                    existing.excluido_por = dto.excluido_por;
                    existing.fecha        = DateTime.UtcNow;
                    existing.motivo       = dto.motivo ?? "Excluido por administrador";
                }
                else
                {
                    _db.BuroExclusiones.Add(new BuroExclusion
                    {
                        cliente_id   = dto.cliente_id,
                        excluido_por = dto.excluido_por,
                        fecha        = DateTime.UtcNow,
                        motivo       = dto.motivo ?? "Excluido por administrador",
                    });
                }
                await _db.SaveChangesAsync();
                return Ok(new { message = "Cliente excluido del buró correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // DELETE /api/BuroExclusion/{clienteId} — volver a habilitar reporte
        // MONEYPINE-FIX: estaba abierto a cualquier rol autenticado. Sacar a un
        // cliente del buro afecta su historial crediticio, asi que solo ADMIN.
        // Los demas roles lo piden via SolicitudAprobacion.
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{clienteId:int}")]
        public async Task<IActionResult> Restaurar(int clienteId)
        {
            try
            {
                var existing = await _db.BuroExclusiones.FindAsync(clienteId);
                if (existing == null) return NotFound();
                _db.BuroExclusiones.Remove(existing);
                await _db.SaveChangesAsync();
                return Ok(new { message = "Cliente habilitado para reporte nuevamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }
}
