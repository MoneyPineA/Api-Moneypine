using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Helpers;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("pago_detalle")]
    public class PagoDetalle : ITenantEntity
    {
        [Key]
        public int pago_detalle_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required]
        public int pago_id { get; set; }

        [Required]
        public int prestamo_id { get; set; }

        public int? periodo_id { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal capital_aplicado { get; set; } = 0;

        [Column(TypeName = "decimal(12,4)")]
        public decimal interes_aplicado { get; set; } = 0;

        [Column(TypeName = "decimal(12,4)")]
        public decimal iva_aplicado { get; set; } = 0;

        [Column(TypeName = "decimal(12,4)")]
        public decimal mora_aplicada { get; set; } = 0;

        // true cuando la acumulación total desde pago_detalle cierra el periodo (estado_pago 3 o 5)
        public bool periodo_cerrado { get; set; } = false;

        [MaxLength(30)]
        public string? tipo_pago { get; set; }

        public DateTime fecha_creacion { get; set; } = TimeHelper.GetMexicoTime();

        [JsonIgnore]
        public Pago Pago { get; set; } = null!;

        [JsonIgnore]
        public Prestamo? Prestamo { get; set; }

        [JsonIgnore]
        public PeriodoAmortizacion? Periodo { get; set; }
    }
}
