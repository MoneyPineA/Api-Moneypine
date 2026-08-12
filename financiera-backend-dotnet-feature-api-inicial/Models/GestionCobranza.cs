using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("gestion_cobranza")]
    public class GestionCobranza : ITenantEntity
    {
        [Key]
        public int gestion_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required]
        public int prestamo_id { get; set; }

        public int? usuario_id { get; set; }

        [Required]
        [MaxLength(2000)]
        public string redaccion { get; set; } = string.Empty;

        [Required]
        public DateTime fecha_gestion { get; set; }

        public DateTime fecha_registro { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        [ForeignKey("prestamo_id")]
        public Prestamo Prestamo { get; set; } = null!;

        [ForeignKey("usuario_id")]
        public Usuario? Gestor { get; set; }
    }
}
