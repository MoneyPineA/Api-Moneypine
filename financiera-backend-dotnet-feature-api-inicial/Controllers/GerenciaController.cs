using ApiEjemplo.Data;
using ApiEjemplo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/Gerencia")]
    public class GerenciaController : ControllerBase
    {
        private readonly AppDbContext _context;
        public GerenciaController(AppDbContext context) { _context = context; }

        // ─────────────────────────────────────────────────────────────────────
        // GERENCIAS
        // ─────────────────────────────────────────────────────────────────────

        // GET api/Gerencia
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var gerencias = await _context.Gerencias
                .Include(g => g.Gerente)
                .OrderByDescending(g => g.es_principal)
                .ThenBy(g => g.nombre)
                .Select(g => new
                {
                    g.gerencia_id,
                    g.nombre,
                    g.codigo,
                    g.es_principal,
                    g.gerente_id,
                    gerente_nombre = g.Gerente != null
                        ? $"{g.Gerente.nombre} {g.Gerente.apellido}"
                        : null
                })
                .ToListAsync();
            return Ok(gerencias);
        }

        // GET api/Gerencia/gerentes  — Lista usuarios con rol GERENTE o ADMIN
        [HttpGet("gerentes")]
        public async Task<IActionResult> GetGerentes()
        {
            var gerentes = await _context.Usuarios
                .Where(u => u.rol == ApiEjemplo.Enums.RolUsuario.GERENTE
                    || u.rol == ApiEjemplo.Enums.RolUsuario.ADMIN)
                .OrderBy(u => u.nombre)
                .Select(u => new { u.usuario_id, nombre = $"{u.nombre} {u.apellido}", u.rol })
                .ToListAsync();
            return Ok(gerentes);
        }

        // POST api/Gerencia
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GerenciaDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.nombre) || string.IsNullOrWhiteSpace(dto.codigo))
                return BadRequest("Nombre y código son requeridos.");

            var gerencia = new Gerencia
            {
                nombre = dto.nombre.Trim().ToUpper(),
                codigo = dto.codigo.Trim().ToUpper()
            };
            _context.Gerencias.Add(gerencia);
            await _context.SaveChangesAsync();
            return Ok(new { gerencia.gerencia_id, gerencia.nombre, gerencia.codigo, gerencia.es_principal, gerencia.gerente_id, gerente_nombre = (string?)null });
        }

        // PUT api/Gerencia/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] GerenciaDto dto)
        {
            var gerencia = await _context.Gerencias.FindAsync(id);
            if (gerencia == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.nombre)) gerencia.nombre = dto.nombre.Trim().ToUpper();
            if (!string.IsNullOrWhiteSpace(dto.codigo)) gerencia.codigo = dto.codigo.Trim().ToUpper();

            await _context.SaveChangesAsync();
            await _context.Entry(gerencia).Reference(g => g.Gerente).LoadAsync();
            return Ok(new
            {
                gerencia.gerencia_id,
                gerencia.nombre,
                gerencia.codigo,
                gerencia.es_principal,
                gerencia.gerente_id,
                gerente_nombre = gerencia.Gerente != null
                    ? $"{gerencia.Gerente.nombre} {gerencia.Gerente.apellido}"
                    : null
            });
        }

        // PUT api/Gerencia/{id}/gerente  — Asigna el gerente encargado (rol GERENTE o ADMIN)
        [HttpPut("{id}/gerente")]
        public async Task<IActionResult> SetGerente(int id, [FromBody] SetGerenteDto dto)
        {
            var gerencia = await _context.Gerencias
                .Include(g => g.Gerente)
                .FirstOrDefaultAsync(g => g.gerencia_id == id);
            if (gerencia == null) return NotFound();

            if (dto.gerente_id.HasValue)
            {
                var usuario = await _context.Usuarios.FindAsync(dto.gerente_id.Value);
                if (usuario == null) return BadRequest("Usuario no encontrado.");
                if (usuario.rol != ApiEjemplo.Enums.RolUsuario.GERENTE
                    && usuario.rol != ApiEjemplo.Enums.RolUsuario.ADMIN)
                    return BadRequest("El usuario debe tener rol GERENTE o ADMIN.");
                gerencia.gerente_id = dto.gerente_id.Value;
            }
            else
            {
                gerencia.gerente_id = null;
            }

            await _context.SaveChangesAsync();
            await _context.Entry(gerencia).Reference(g => g.Gerente).LoadAsync();

            return Ok(new
            {
                gerencia.gerencia_id,
                gerencia.nombre,
                gerencia.codigo,
                gerencia.es_principal,
                gerencia.gerente_id,
                gerente_nombre = gerencia.Gerente != null
                    ? $"{gerencia.Gerente.nombre} {gerencia.Gerente.apellido}"
                    : null
            });
        }

        // PUT api/Gerencia/{id}/principal  — Marca una gerencia como principal (desactiva las demás)
        [HttpPut("{id}/principal")]
        public async Task<IActionResult> SetPrincipal(int id)
        {
            var all = await _context.Gerencias.ToListAsync();
            var target = all.FirstOrDefault(g => g.gerencia_id == id);
            if (target == null) return NotFound();

            foreach (var g in all)
                g.es_principal = (g.gerencia_id == id);

            await _context.SaveChangesAsync();
            return Ok(new { target.gerencia_id, target.nombre, target.codigo, es_principal = true });
        }

        // DELETE api/Gerencia/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var gerencia = await _context.Gerencias.FindAsync(id);
            if (gerencia == null) return NotFound();

            _context.Gerencias.Remove(gerencia);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ─────────────────────────────────────────────────────────────────────
        // RUTAS
        // ─────────────────────────────────────────────────────────────────────

        // GET api/Gerencia/rutas
        [HttpGet("rutas")]
        public async Task<IActionResult> GetRutas()
        {
            var rutas = await _context.Rutas
                .Include(r => r.Gerencia)
                .Include(r => r.Asesor)
                .OrderBy(r => r.Gerencia.nombre)
                .ThenBy(r => r.nombre)
                .Select(r => new
                {
                    r.ruta_id,
                    r.codigo,
                    r.nombre,
                    r.gerencia_id,
                    gerencia_nombre = r.Gerencia.nombre,
                    r.asesor_id,
                    asesor_nombre = r.Asesor != null
                        ? $"{r.Asesor.nombre} {r.Asesor.apellido}"
                        : null
                })
                .ToListAsync();
            return Ok(rutas);
        }

        // POST api/Gerencia/rutas
        [HttpPost("rutas")]
        public async Task<IActionResult> CreateRuta([FromBody] RutaDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.nombre) || string.IsNullOrWhiteSpace(dto.codigo))
                return BadRequest("Nombre y código son requeridos.");

            var gerenciaExists = await _context.Gerencias.AnyAsync(g => g.gerencia_id == dto.gerencia_id);
            if (!gerenciaExists) return BadRequest("Gerencia no encontrada.");

            var ruta = new Ruta
            {
                codigo     = dto.codigo.Trim().ToUpper(),
                nombre     = dto.nombre.Trim().ToUpper(),
                gerencia_id = dto.gerencia_id,
                asesor_id  = dto.asesor_id
            };
            _context.Rutas.Add(ruta);
            await _context.SaveChangesAsync();

            await _context.Entry(ruta).Reference(r => r.Gerencia).LoadAsync();
            if (ruta.asesor_id.HasValue)
                await _context.Entry(ruta).Reference(r => r.Asesor).LoadAsync();

            return Ok(new
            {
                ruta.ruta_id,
                ruta.codigo,
                ruta.nombre,
                ruta.gerencia_id,
                gerencia_nombre = ruta.Gerencia.nombre,
                ruta.asesor_id,
                asesor_nombre = ruta.Asesor != null ? $"{ruta.Asesor.nombre} {ruta.Asesor.apellido}" : null
            });
        }

        // PUT api/Gerencia/rutas/{id}
        [HttpPut("rutas/{id}")]
        public async Task<IActionResult> UpdateRuta(int id, [FromBody] RutaDto dto)
        {
            var ruta = await _context.Rutas
                .Include(r => r.Gerencia)
                .Include(r => r.Asesor)
                .FirstOrDefaultAsync(r => r.ruta_id == id);
            if (ruta == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.codigo)) ruta.codigo = dto.codigo.Trim().ToUpper();
            if (!string.IsNullOrWhiteSpace(dto.nombre)) ruta.nombre = dto.nombre.Trim().ToUpper();
            if (dto.gerencia_id > 0)
            {
                var exists = await _context.Gerencias.AnyAsync(g => g.gerencia_id == dto.gerencia_id);
                if (exists) ruta.gerencia_id = dto.gerencia_id;
            }
            ruta.asesor_id = dto.asesor_id;

            await _context.SaveChangesAsync();

            await _context.Entry(ruta).Reference(r => r.Gerencia).LoadAsync();
            if (ruta.asesor_id.HasValue)
                await _context.Entry(ruta).Reference(r => r.Asesor).LoadAsync();

            return Ok(new
            {
                ruta.ruta_id,
                ruta.codigo,
                ruta.nombre,
                ruta.gerencia_id,
                gerencia_nombre = ruta.Gerencia.nombre,
                ruta.asesor_id,
                asesor_nombre = ruta.Asesor != null ? $"{ruta.Asesor.nombre} {ruta.Asesor.apellido}" : null
            });
        }

        // DELETE api/Gerencia/rutas/{id}
        [HttpDelete("rutas/{id}")]
        public async Task<IActionResult> DeleteRuta(int id)
        {
            var ruta = await _context.Rutas.FindAsync(id);
            if (ruta == null) return NotFound();

            _context.Rutas.Remove(ruta);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    // ─── DTOs ───────────────────────────────────────────────────────────────
    public record GerenciaDto(string nombre, string codigo);
    public record RutaDto(string codigo, string nombre, int gerencia_id, int? asesor_id);
    public record SetGerenteDto(int? gerente_id);
}
