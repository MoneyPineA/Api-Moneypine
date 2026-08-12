using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ApiEjemplo.Data;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConfiguracionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenant;

        public ConfiguracionController(AppDbContext context, ITenantContext tenant)
        {
            _context = context;
            _tenant = tenant;
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
                // MONEYPINE-MT: filtro manual, SQL crudo. Esta tabla no tiene entidad
                // EF, asi que el query filter global no la alcanza: el tenant va
                // explicito o un prestamista leeria la configuracion de otro.
                cmd.CommandText =
                    "SELECT clave, valor FROM configuracion_sistema " +
                    "WHERE clave = @clave AND prestamista_id = @tenant";

                var p = cmd.CreateParameter();
                p.ParameterName = "@clave";
                p.Value = clave;
                cmd.Parameters.Add(p);

                var pt = cmd.CreateParameter();
                pt.ParameterName = "@tenant";
                pt.Value = _tenant.PrestamistaId;
                cmd.Parameters.Add(pt);

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
                // MONEYPINE-MT: filtro manual, SQL crudo. Al quitar el DEFAULT de
                // prestamista_id este INSERT empezo a fallar con 500 ("Field
                // 'prestamista_id' doesn't have a default value") para TODA clave,
                // porque MySQL evalua el INSERT antes de decidir el ON DUPLICATE KEY.
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO configuracion_sistema (clave, valor, prestamista_id) " +
                    "VALUES ({0}, {1}, {2}) " +
                    "ON DUPLICATE KEY UPDATE valor = VALUES(valor)",
                    clave, dto.valor, _tenant.PrestamistaId);

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
