using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("gastos_recientes")]
    public class GastoReciente : ITenantEntity
    {
        [Key]
        public int gasto_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required]
        [MaxLength(150)]
        public string concepto { get; set; } = string.Empty;

        [Required]
        public DateTime fecha_gasto { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto { get; set; }
    }
}
