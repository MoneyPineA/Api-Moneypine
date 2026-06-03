using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ApiEjemplo.Models
{
    // MONEYPINE-FIX: tabla de amortización migrada desde sinaits5_moneypine.credito_amortizacion
    [Table("periodo_amortizacion")]
    public class PeriodoAmortizacion
    {
        [Key]
        public int periodo_id { get; set; }

        [Required]
        public int prestamo_id { get; set; }

        [Required]
        public int periodo { get; set; }

        [Required]
        public DateTime fecha_inicio { get; set; }

        [Required]
        public DateTime fecha_vencimiento { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal capital_pendiente { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal abono_capital { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal interes_normal { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal interes_iva { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal ahorro_por_pago { get; set; }

        // Valor guardado en BD — usado para mora congelada (estado_pago = 5)
        [Column(TypeName = "decimal(16,4)")]
        public decimal interes_moratorio { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal gasto_cobranza { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal pago_pactado { get; set; }

        [Column(TypeName = "decimal(16,4)")]
        public decimal saldo_final { get; set; }

        public DateTime? fecha_pagado { get; set; }

        public int dias_moratorio { get; set; }

        // 1=pendiente, 2=pagado normal, 3=pagado(ahorroPorPago), 5=mora congelada
        public int estado_pago { get; set; }

        [ForeignKey("prestamo_id")]
        [JsonIgnore]
        public Prestamo? Prestamo { get; set; }
    }
}
