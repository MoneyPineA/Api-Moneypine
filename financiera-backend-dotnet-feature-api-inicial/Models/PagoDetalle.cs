using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ApiEjemplo.Models
{
    [Table("pago_detalle")]
    public class PagoDetalle
    {
        [Key]
        public int pago_detalle_id { get; set; }

        [Required]
        public int pago_id { get; set; }

        // Null para registros que no afectan un periodo específico (e.g. ahorro_por_pago global)
        public int? periodo_id { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal capital_aplicado { get; set; } = 0;

        [Column(TypeName = "decimal(12,4)")]
        public decimal interes_aplicado { get; set; } = 0;

        [Column(TypeName = "decimal(12,4)")]
        public decimal iva_aplicado { get; set; } = 0;

        [Column(TypeName = "decimal(12,4)")]
        public decimal mora_aplicada { get; set; } = 0;

        // true si el pago cerró el periodo (estado_pago 2/3/5); false si fue parcial (ahorro_por_pago, capital parcial)
        public bool periodo_cerrado { get; set; } = false;

        [MaxLength(30)]
        public string? tipo_pago { get; set; }

        [JsonIgnore]
        public Pago Pago { get; set; } = null!;

        [JsonIgnore]
        public PeriodoAmortizacion? Periodo { get; set; }
    }
}
