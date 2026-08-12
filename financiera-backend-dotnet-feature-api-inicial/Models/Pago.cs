using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Enums;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("pago")]
    public class Pago : ITenantEntity
    {
        [Key]
        public int pago_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3. Redundante con prestamo.prestamista_id
        // a propósito: evita que el aislamiento dependa de un JOIN bien escrito.
        public int prestamista_id { get; set; }

        [Required]
        public int prestamo_id { get; set; }

        public int? cobrador_id { get; set; }

        // Fechas
        [Required]
        public DateTime fecha_pago { get; set; }

        // Montos
        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto_pagado { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal interes_pagado { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal mora_pagada { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal saldo_restante { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal abono_capital { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal interes_iva { get; set; } = 0; // MONEYPINE-FIX: IVA sobre interés

        // Método
        public string? metodo_pago { get; set; }

        [MaxLength(30)]
        public string? tipo_pago { get; set; }

        // Estatus (ENUM)
        [Required]
        public EstatusPago estatus { get; set; } = EstatusPago.APLICADO;

        // Relaciones
        [JsonIgnore]
        public Prestamo Prestamo { get; set; } = null!;
    }
}