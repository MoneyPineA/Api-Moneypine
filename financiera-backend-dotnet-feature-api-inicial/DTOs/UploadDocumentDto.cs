namespace ApiEjemplo.DTOs
{
    public class UploadDocumentDto
    {
        public IFormFile? File { get; set; }

        public string? DocumentType { get; set; }

        public int ClienteId { get; set; }

        public int? PrestamoId { get; set; }
    }
}