using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("ruta")]
    public class Ruta : ITenantEntity
    {
        [Key]
        public int ruta_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required, MaxLength(30)]
        public string codigo { get; set; } = "";

        [Required, MaxLength(200)]
        public string nombre { get; set; } = "";

        public int gerencia_id { get; set; }

        [ForeignKey("gerencia_id")]
        [JsonIgnore]
        public Gerencia Gerencia { get; set; } = null!;

        public int? asesor_id { get; set; }

        [ForeignKey("asesor_id")]
        [JsonIgnore]
        public Usuario? Asesor { get; set; }
    }
}
