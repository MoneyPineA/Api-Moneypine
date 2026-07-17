using ApiEjemplo.Data;
using ApiEjemplo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ApiEjemplo.Controllers
{
    // Formatos de Documentos (Utilerías): plantillas oficiales.
    // Subir/eliminar: SOLO ADMIN. Listar/descargar: cualquier usuario autenticado.
    // Archivos en BLOB (MySQL) — el disco de Railway se borra en cada deploy.
    [ApiController]
    [Route("api/[controller]")]
    public class FormatoController : ControllerBase
    {
        private readonly AppDbContext _context;

        private static readonly string[] MimePermitidos =
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "image/jpeg",
            "image/png",
        };

        private const long MaxArchivo = 10 * 1024 * 1024;  // 10 MB
        private const long MaxIcono   = 512 * 1024;        // 512 KB

        public FormatoController(AppDbContext context)
        {
            _context = context;
        }

        // ============================
        // GET: api/Formato
        // Lista de formatos (sin el contenido binario) — cualquier autenticado
        // ============================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var formatos = await _context.FormatosDocumentos
                .OrderBy(f => f.categoria).ThenBy(f => f.nombre)
                .Select(f => new
                {
                    f.formato_id,
                    f.nombre,
                    f.categoria,
                    f.descripcion,
                    f.nombre_archivo,
                    f.tipo_mime,
                    f.tamano_bytes,
                    f.fecha_subida,
                    autor = f.Usuario != null ? $"{f.Usuario.nombre} {f.Usuario.apellido}".Trim() : null,
                    icono_base64 = f.icono != null && f.icono_mime != null
                        ? $"data:{f.icono_mime};base64,{Convert.ToBase64String(f.icono)}"
                        : null,
                })
                .ToListAsync();

            return Ok(formatos);
        }

        // ============================
        // GET: api/Formato/{id}/descargar — cualquier autenticado
        // ============================
        [Authorize]
        [HttpGet("{id}/descargar")]
        public async Task<IActionResult> Descargar(int id)
        {
            var formato = await _context.FormatosDocumentos.FindAsync(id);
            if (formato == null)
                return NotFound("Formato no encontrado");

            return File(formato.contenido, formato.tipo_mime, formato.nombre_archivo);
        }

        // ============================
        // POST: api/Formato (multipart/form-data) — SOLO ADMIN
        // ============================
        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> Create([FromForm] FormatoCreateDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return BadRequest("El nombre del formato es obligatorio");

            if (dto.Archivo == null || dto.Archivo.Length == 0)
                return BadRequest("El archivo del formato es obligatorio");

            if (dto.Archivo.Length > MaxArchivo)
                return BadRequest("El archivo supera el tamaño máximo de 10MB");

            if (!MimePermitidos.Contains(dto.Archivo.ContentType))
                return BadRequest($"Tipo de archivo no permitido ({dto.Archivo.ContentType}). Usa PDF, Word, Excel o imagen.");

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var usuarioId))
                return Unauthorized("Token sin identificador de usuario");

            byte[] contenido;
            using (var ms = new MemoryStream())
            {
                await dto.Archivo.CopyToAsync(ms);
                contenido = ms.ToArray();
            }

            byte[]? icono = null;
            string? iconoMime = null;
            if (dto.Icono != null && dto.Icono.Length > 0)
            {
                if (dto.Icono.Length > MaxIcono)
                    return BadRequest("El icono supera el tamaño máximo de 512KB");
                if (!dto.Icono.ContentType.StartsWith("image/"))
                    return BadRequest("El icono debe ser una imagen");

                using var ms = new MemoryStream();
                await dto.Icono.CopyToAsync(ms);
                icono = ms.ToArray();
                iconoMime = dto.Icono.ContentType;
            }

            var formato = new FormatoDocumento
            {
                nombre         = dto.Nombre.Trim(),
                categoria      = string.IsNullOrWhiteSpace(dto.Categoria) ? "Solicitudes" : dto.Categoria!.Trim(),
                descripcion    = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion!.Trim(),
                nombre_archivo = Path.GetFileName(dto.Archivo.FileName),
                tipo_mime      = dto.Archivo.ContentType,
                tamano_bytes   = dto.Archivo.Length,
                contenido      = contenido,
                icono          = icono,
                icono_mime     = iconoMime,
                usuario_id     = usuarioId,
            };

            _context.FormatosDocumentos.Add(formato);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new
            {
                formato.formato_id,
                formato.nombre,
                formato.categoria,
                formato.nombre_archivo,
                formato.tamano_bytes,
            });
        }

        // ============================
        // DELETE: api/Formato/{id} — SOLO ADMIN
        // ============================
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var formato = await _context.FormatosDocumentos.FindAsync(id);
            if (formato == null)
                return NotFound("Formato no encontrado");

            _context.FormatosDocumentos.Remove(formato);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class FormatoCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Categoria { get; set; }
        public string? Descripcion { get; set; }
        public IFormFile? Archivo { get; set; }
        public IFormFile? Icono { get; set; }
    }
}
