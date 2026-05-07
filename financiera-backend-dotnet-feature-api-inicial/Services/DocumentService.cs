using ApiEjemplo.Data;
using ApiEjemplo.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    public class DocumentService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private readonly string[] allowedMimeTypes =
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        private const long maxFileSize = 10 * 1024 * 1024;

        public DocumentService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<Documento> UploadDocument(
            IFormFile file,
            int clienteId,
            int? prestamoId,
            int usuarioId,
            string documentType
        )
        {
            if (file == null || file.Length == 0)
                throw new Exception("Archivo vacío");

            if (file.Length > maxFileSize)
                throw new Exception("Archivo supera el tamaño máximo de 10MB");

            if (!allowedMimeTypes.Contains(file.ContentType))
                throw new Exception("Tipo de archivo no permitido");

            var extension = Path.GetExtension(file.FileName);

            var safeFileName = $"{Guid.NewGuid()}{extension}";

            var folder = Path.Combine(_env.ContentRootPath, "uploads");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var filePath = Path.Combine(folder, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var documento = new Documento
            {
                ClienteId = clienteId,
                PrestamoId = prestamoId,
                UsuarioId = usuarioId,
                NombreOriginal = file.FileName,
                NombreArchivo = safeFileName,
                TipoMime = file.ContentType,
                TipoDocumento = documentType,
                TamanoBytes = file.Length,
                RutaArchivo = filePath,
                FechaSubida = DateTime.UtcNow
            };

            _context.Documentos.Add(documento);

            await _context.SaveChangesAsync();

            return documento;
        }
        public async Task<bool> DeleteDocument(int documentoId)
        {
            var documento = await _context.Documentos
                .FirstOrDefaultAsync(d => d.DocumentoId == documentoId);

            if (documento == null)
                return false;

            if (File.Exists(documento.RutaArchivo))
                File.Delete(documento.RutaArchivo);

            _context.Documentos.Remove(documento);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}