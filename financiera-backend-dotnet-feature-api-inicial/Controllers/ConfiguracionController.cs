using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ApiEjemplo.Data;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfiguracionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConfiguracionController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/Configuracion/{clave}
        [HttpGet("{clave}")]
        public async Task<IActionResult> Get(string clave)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT clave, valor FROM configuracion_sistema WHERE clave = @clave";
                var p = cmd.CreateParameter();
                p.ParameterName = "@clave";
                p.Value = clave;
                cmd.Parameters.Add(p);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { clave, mensaje = "Clave no encontrada" });

                return Ok(new { clave = reader.GetString(0), valor = reader.GetString(1) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // PUT /api/Configuracion/{clave}  body: { valor: "true" | "false" | ... }
        [HttpPut("{clave}")]
        public async Task<IActionResult> Put(string clave, [FromBody] ConfiguracionValorDto dto)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO configuracion_sistema (clave, valor) VALUES ({0}, {1}) " +
                    "ON DUPLICATE KEY UPDATE valor = VALUES(valor)",
                    clave, dto.valor);

                return Ok(new { clave, valor = dto.valor });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }

    // MONEYPINE-FIX: DTO mínimo para PUT /api/Configuracion/{clave}
    public class ConfiguracionValorDto
    {
        public string valor { get; set; } = "";
    }
}
