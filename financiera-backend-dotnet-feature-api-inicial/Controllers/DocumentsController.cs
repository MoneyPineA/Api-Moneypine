using ApiEjemplo.DTOs;
using ApiEjemplo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly DocumentService _documentService;

        public DocumentsController(DocumentService documentService)
        {
            _documentService = documentService;
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentDto dto)
        {
            if (dto.File == null)
                return BadRequest("Archivo requerido");

            if (dto.DocumentType == null)
                return BadRequest("Tipo de documento requerido");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim.Value);

            var document = await _documentService.UploadDocument(
                dto.File,
                dto.ClienteId,
                dto.PrestamoId,
                userId,
                dto.DocumentType
            );

            return Ok(new
            {
                id = document.DocumentoId,
                file_name = document.NombreOriginal,
                file_type = document.TipoMime,
                file_url = $"/documents/{document.NombreArchivo}",
                uploaded_at = document.FechaSubida
            });
        }
        
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _documentService.DeleteDocument(id);

            if (!deleted)
                return NotFound("Documento no encontrado");

            return Ok(new
            {
                message = "Documento eliminado correctamente"
            });
        }
    }
}