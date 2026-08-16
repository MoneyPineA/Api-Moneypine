using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("producto_credito")]
    public class ProductoCredito : ITenantEntity
    {
        [Key]
        public int id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required]
        [MaxLength(20)]
        public string tipo_credito { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string nombre { get; set; } = string.Empty;

        [Column(TypeName = "decimal(12,2)")]
        public decimal monto_base { get; set; } = 0;

        [MaxLength(20)]
        public string? forma_pago { get; set; }

        public int plazo { get; set; } = 0;

        [Column(TypeName = "decimal(6,2)")]
        public decimal tasa_interes { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal mora_diaria { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal comision_apertura { get; set; } = 0;

        public bool es_defecto { get; set; } = false;

        public bool activo { get; set; } = true;

        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}
