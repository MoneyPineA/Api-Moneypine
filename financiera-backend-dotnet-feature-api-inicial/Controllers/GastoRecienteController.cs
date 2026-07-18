using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GastoRecienteController : ControllerBase
    {
        private readonly AppDbContext _db;

        public GastoRecienteController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/GastoReciente — todos los gastos registrados, mas recientes primero
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var lista = await _db.GastosRecientes
                .OrderByDescending(g => g.fecha_gasto)
                .ThenByDescending(g => g.gasto_id)
                .Select(g => new
                {
                    g.gasto_id,
                    g.concepto,
                    fecha_gasto = g.fecha_gasto.ToString("yyyy-MM-dd"),
                    g.monto,
                })
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/GastoReciente — registrar un nuevo gasto del banco
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] GastoReciente dto)
        {
            if (string.IsNullOrWhiteSpace(dto.concepto))
                return BadRequest("concepto es requerido");
            if (dto.monto <= 0)
                return BadRequest("monto debe ser mayor a 0");

            var gasto = new GastoReciente
            {
                concepto    = dto.concepto.Trim(),
                fecha_gasto = dto.fecha_gasto == default ? DateTime.UtcNow.Date : dto.fecha_gasto,
                monto       = dto.monto,
            };

            _db.GastosRecientes.Add(gasto);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                gasto.gasto_id,
                gasto.concepto,
                fecha_gasto = gasto.fecha_gasto.ToString("yyyy-MM-dd"),
                gasto.monto,
            });
        }
    }
}
