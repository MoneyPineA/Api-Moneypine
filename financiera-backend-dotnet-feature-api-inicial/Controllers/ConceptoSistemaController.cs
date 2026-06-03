using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConceptoSistemaController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ConceptoSistemaController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/ConceptoSistema  — solo activos, ordenados tipo/orden
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var conceptos = await _db.ConceptosSistema
                    .Where(c => c.activo)
                    .OrderBy(c => c.tipo)
                    .ThenBy(c => c.orden)
                    .ToListAsync();
                return Ok(conceptos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // POST /api/ConceptoSistema
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ConceptoSistema dto)
        {
            try
            {
                dto.id = 0;
                dto.created_at = DateTime.UtcNow;
                dto.activo = true;
                _db.ConceptosSistema.Add(dto);
                await _db.SaveChangesAsync();
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // PUT /api/ConceptoSistema/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ConceptoSistema dto)
        {
            try
            {
                var existing = await _db.ConceptosSistema.FindAsync(id);
                if (existing == null) return NotFound();
                existing.nombre = dto.nombre;
                existing.tipo   = dto.tipo;
                existing.orden  = dto.orden;
                await _db.SaveChangesAsync();
                return Ok(existing);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // DELETE /api/ConceptoSistema/{id} — soft delete
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _db.ConceptosSistema.FindAsync(id);
                if (existing == null) return NotFound();
                existing.activo = false;
                await _db.SaveChangesAsync();
                return Ok(new { message = "Concepto eliminado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }
}
