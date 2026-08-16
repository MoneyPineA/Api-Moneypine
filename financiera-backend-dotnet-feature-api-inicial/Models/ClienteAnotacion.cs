using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Helpers;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("cliente_anotacion")]
    public class ClienteAnotacion : ITenantEntity
    {
        [Key]
        public int anotacion_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required]
        public int cliente_id { get; set; }

        // Usuario que escribió la anotación
        [Required]
        public int usuario_id { get; set; }

        [MaxLength(50)]
        public string origen { get; set; } = "MANUAL";

        [Required]
        public string anotacion { get; set; } = string.Empty;

        public DateTime fecha_creacion { get; set; } = TimeHelper.GetMexicoTime();

        [JsonIgnore]
        public Cliente? Cliente { get; set; }

        [JsonIgnore]
        public Usuario? Usuario { get; set; }
    }
}
