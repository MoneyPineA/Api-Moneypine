using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Helpers;

namespace ApiEjemplo.Models
{
    // Plantillas oficiales (Utilerías → Formatos de Documentos).
    // El archivo se guarda como BLOB en MySQL: el filesystem de Railway es
    // efímero (cada deploy lo borra), la BD es el único almacenamiento durable.
    [Table("formato_documento")]
    public class FormatoDocumento
    {
        [Key]
        public int formato_id { get; set; }

        [Required, MaxLength(150)]
        public string nombre { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string categoria { get; set; } = "Solicitudes";

        [MaxLength(300)]
        public string? descripcion { get; set; }

        [Required, MaxLength(255)]
        public string nombre_archivo { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string tipo_mime { get; set; } = string.Empty;

        public long tamano_bytes { get; set; }

        [Required]
        public byte[] contenido { get; set; } = Array.Empty<byte>();

        // Icono opcional de la tarjeta (imagen pequeña)
        public byte[]? icono { get; set; }

        [MaxLength(100)]
        public string? icono_mime { get; set; }

        // Admin que subió el formato
        [Required]
        public int usuario_id { get; set; }

        public DateTime fecha_subida { get; set; } = TimeHelper.GetMexicoTime();

        [JsonIgnore]
        public Usuario? Usuario { get; set; }
    }
}
