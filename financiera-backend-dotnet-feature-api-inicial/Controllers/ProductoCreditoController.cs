using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductoCreditoController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductoCreditoController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // GET: api/ProductoCredito
        // Devuelve todos los productos activos
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var productos = await _context.ProductosCredito
                .Where(p => p.activo)
                .OrderBy(p => p.id)
                .ToListAsync();
            return Ok(productos);
        }

        // =====================================================
        // POST: api/ProductoCredito
        // Crea un nuevo producto
        // =====================================================
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductoCredito dto)
        {
            dto.id         = 0;
            dto.activo     = true;
            dto.created_at = DateTime.UtcNow;
            _context.ProductosCredito.Add(dto);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = dto.id }, dto);
        }

        // =====================================================
        // PUT: api/ProductoCredito/{id}
        // Actualiza un producto existente
        // =====================================================
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductoCredito dto)
        {
            var existing = await _context.ProductosCredito.FindAsync(id);
            if (existing == null) return NotFound("Producto no encontrado");

            existing.tipo_credito      = dto.tipo_credito;
            existing.nombre            = dto.nombre;
            existing.monto_base        = dto.monto_base;
            existing.forma_pago        = dto.forma_pago;
            existing.plazo             = dto.plazo;
            existing.tasa_interes      = dto.tasa_interes;
            existing.mora_diaria       = dto.mora_diaria;
            existing.comision_apertura = dto.comision_apertura;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        // =====================================================
        // DELETE: api/ProductoCredito/{id}
        // Soft delete: activo = false
        // =====================================================
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _context.ProductosCredito.FindAsync(id);
            if (existing == null) return NotFound("Producto no encontrado");

            existing.activo     = false;
            existing.es_defecto = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =====================================================
        // PATCH: api/ProductoCredito/{id}/defecto
        // Marca un producto como defecto, desmarca los demás
        // =====================================================
        [Authorize]
        [HttpPatch("{id}/defecto")]
        public async Task<IActionResult> SetDefecto(int id)
        {
            var producto = await _context.ProductosCredito.FindAsync(id);
            if (producto == null || !producto.activo)
                return NotFound("Producto no encontrado o inactivo");

            var actuales = await _context.ProductosCredito
                .Where(p => p.activo && p.es_defecto)
                .ToListAsync();
            foreach (var p in actuales)
                p.es_defecto = false;

            producto.es_defecto = true;
            await _context.SaveChangesAsync();
            return Ok(producto);
        }
    }
}
