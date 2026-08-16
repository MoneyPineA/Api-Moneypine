using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    // MONEYPINE-FIX: tabla de amortización migrada desde sinaits5_moneypine.credito_amortizacion
    [Table("periodo_amortizacion")]
    public class PeriodoAmortizacion : ITenantEntity
    {
        [Key]
        public int periodo_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

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

        // MONEYPINE-FIX: mora condonada acumulada de este periodo. A diferencia de
        // interes_moratorio (que MotorRecalculoPrestamoService.Reconstruir recalcula
        // desde cero en cada pago/cron), este campo es durable — Reconstruir lo resta
        // de la mora bruta recalculada en vez de resetearlo, para que una condonación
        // parcial no reaparezca en el siguiente pago o barrido diario.
        [Column(TypeName = "decimal(16,4)")]
        public decimal mora_condonada { get; set; } = 0;

        // MONEYPINE-FIX: condonación de crédito (capital/interés/IVA) — mismo patrón durable
        // que mora_condonada. Reconstruir() sembra sus acumuladores capC/intC/ivaC con estos
        // valores en vez de resetearlos a 0, para que una condonación de crédito no reaparezca.
        [Column(TypeName = "decimal(16,4)")]
        public decimal capital_condonado { get; set; } = 0;

        [Column(TypeName = "decimal(16,4)")]
        public decimal interes_condonado { get; set; } = 0;

        [Column(TypeName = "decimal(16,4)")]
        public decimal iva_condonado { get; set; } = 0;

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
