using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ApiEjemplo.Data;
using ApiEjemplo.Models;
using ApiEjemplo.Services;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ListaNegraController : ControllerBase
    {
        private readonly AppDbContext      _db;
        private readonly ListaNegraService _svc;

        public ListaNegraController(AppDbContext db, ListaNegraService svc)
        {
            _db  = db;
            _svc = svc;
        }

        private int? UsuarioActual()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }

        // GET /api/ListaNegra — lista activa desde BD
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var activos = await _db.ListasNegras
                    .Where(ln => ln.estado == "ACTIVO")
                    .Join(_db.Clientes,
                          ln => ln.cliente_id,
                          c  => c.cliente_id,
                          (ln, c) => new { ln, c })
                    .Join(_db.Usuarios,
                          lnc => lnc.c.usuario_id,
                          u   => u.usuario_id,
                          (lnc, u) => new
                          {
                              lnc.ln.lista_negra_id,
                              lnc.ln.cliente_id,
                              lnc.ln.prestamo_id,
                              nombre_cliente    = u.nombre,
                              apellido_paterno  = lnc.c.apellido_paterno,
                              apellido_materno  = lnc.c.apellido_materno,
                              lnc.ln.motivo,
                              lnc.ln.dias_mora,
                              lnc.ln.monto_mora,
                              lnc.ln.estado,
                              lnc.ln.origen,
                              fecha_alta        = lnc.ln.fecha_alta.ToString("yyyy-MM-dd HH:mm"),
                              lnc.ln.observaciones,
                          })
                    .OrderByDescending(x => x.dias_mora)
                    .ToListAsync();

                return Ok(activos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // GET /api/ListaNegra/cliente/{clienteId} — consulta rápida por cliente (para NuevaSolicitudCredito)
        [HttpGet("cliente/{clienteId:int}")]
        [Authorize]
        public async Task<IActionResult> GetByCliente(int clienteId)
        {
            try
            {
                var enLista = await _db.ListasNegras
                    .AnyAsync(ln => ln.cliente_id == clienteId && ln.estado == "ACTIVO");
                return Ok(new { cliente_id = clienteId, en_lista_negra = enLista });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // GET /api/ListaNegra/candidatos — préstamos que cumplen criterios (mora > 130d y > $1500)
        [HttpGet("candidatos")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetCandidatos()
        {
            try
            {
                var candidatos = await _svc.ObtenerCandidatosAsync();
                return Ok(candidatos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // POST /api/ListaNegra — agregar manualmente (solo ADMIN)
        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Agregar([FromBody] AgregarListaNegraRequest req)
        {
            try
            {
                if (req.cliente_id <= 0)
                    return BadRequest(new { error = "cliente_id requerido." });

                var clienteExiste = await _db.Clientes.AnyAsync(c => c.cliente_id == req.cliente_id);
                if (!clienteExiste)
                    return NotFound(new { error = $"Cliente {req.cliente_id} no encontrado." });

                if (req.prestamo_id.HasValue)
                {
                    var prestamo = await _db.Prestamos.FindAsync(req.prestamo_id.Value);
                    if (prestamo == null)
                        return NotFound(new { error = $"Préstamo {req.prestamo_id} no encontrado." });

                    bool esCandidato = await _svc.EsCandidatoAsync(prestamo);
                    if (!esCandidato && req.origen != "MANUAL")
                        return BadRequest(new { error = "El préstamo no cumple los criterios de lista negra (mora > 130 días y > $1500)." });
                }

                var (ok, msg) = await _svc.AgregarAsync(
                    req.cliente_id,
                    req.prestamo_id,
                    req.motivo ?? "Agregado manualmente por administrador",
                    req.origen ?? "MANUAL",
                    UsuarioActual(),
                    req.observaciones);

                if (!ok) return Conflict(new { error = msg });
                return Ok(new { message = msg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // PUT /api/ListaNegra/{id}/remover — marcar REMOVIDO (no borra físico)
        [HttpPut("{id:int}/remover")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Remover(int id)
        {
            try
            {
                var ok = await _svc.RemoverAsync(id, UsuarioActual());
                if (!ok) return NotFound(new { error = $"Entrada {id} no encontrada." });
                return Ok(new { message = "Cliente removido de lista negra." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // POST /api/ListaNegra/sincronizar — proceso automático (solo ADMIN)
        [HttpPost("sincronizar")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Sincronizar()
        {
            try
            {
                var resultado = await _svc.SincronizarAsync(UsuarioActual());

                var mensaje = $"Sincronización completada: {resultado.agregados} agregados, " +
                              $"{resultado.removidos} removidos.";
                if (resultado.omitidos_por_baja_manual > 0)
                    mensaje += $" {resultado.omitidos_por_baja_manual} omitido(s) por baja manual previa.";

                return Ok(new
                {
                    message   = mensaje,
                    agregados = resultado.agregados,
                    removidos = resultado.removidos,
                    omitidos_por_baja_manual = resultado.omitidos_por_baja_manual,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }

    public class AgregarListaNegraRequest
    {
        public int     cliente_id    { get; set; }
        public int?    prestamo_id   { get; set; }
        public string? motivo        { get; set; }
        public string? origen        { get; set; }
        public string? observaciones { get; set; }
    }
}
