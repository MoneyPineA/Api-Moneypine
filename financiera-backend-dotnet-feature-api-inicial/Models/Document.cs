using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("documento")]
    public class Documento : ITenantEntity
    {
        [Key]
        [Column("documento_id")]
        public int DocumentoId { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        [Column("prestamista_id")]
        public int prestamista_id { get; set; }

        [Column("cliente_id")]
        public int ClienteId { get; set; }

        [Column("prestamo_id")]
        public int? PrestamoId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Column("nombre_original")]
        public string NombreOriginal { get; set; } = null!;

        [Column("nombre_archivo")]
        public string NombreArchivo { get; set; } = null!;

        [Column("tipo_mime")]
        public string TipoMime { get; set; } = null!;

        [Column("tipo_documento")]
        public string TipoDocumento { get; set; } = null!;

        [Column("tamano_bytes")]
        public long TamanoBytes { get; set; }

        [Column("ruta_archivo")]
        public string RutaArchivo { get; set; } = null!;

        [Column("fecha_subida")]
        public DateTime FechaSubida { get; set; }
    }
}