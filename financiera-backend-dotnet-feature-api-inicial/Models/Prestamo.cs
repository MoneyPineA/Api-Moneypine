using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    [Table("prestamo")]
    public class Prestamo
    {
        [Key]
        public int prestamo_id { get; set; }

        [Required]
        public int cliente_id { get; set; }

        // Montos
        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto_total { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal saldo_actual { get; set; }

        // Condiciones
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal tasa_interes { get; set; }

        [Required]
        public int plazo_meses { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal pago_mes { get; set; }

        // Fechas
        [Required]
        public DateTime fecha_inicio { get; set; }

        public DateTime? fecha_proximo_pago { get; set; }

        public DateTime? fecha_fin { get; set; }

        public DateTime fecha_creacion { get; set; }

        // Forma de pago (ENUM)
        [Required]
        public FormasPago forma_pago { get; set; } = FormasPago.MENSUAL;

        // Cobrador asignado (opcional)
        public int? cobrador_id { get; set; }

        [ForeignKey("cobrador_id")]
        [JsonIgnore]
        public Usuario? Cobrador { get; set; }

        // Estatus (ENUM)
        [Required]
        public EstatusPrestamo estatus { get; set; } = EstatusPrestamo.ACTIVO;

        // Mora
        public int dias_gracia { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal mora_diaria { get; set; } = 0;

        // MONEYPINE-FIX: tasa anual moratorio (tasaM del producto fuente, ej. 50%)
        [Column(TypeName = "decimal(10,4)")]
        public decimal tasa_moratorio_anual { get; set; } = 0;

        // Campos CNBV / producto
        [MaxLength(50)]
        public string? tipo_cnbv { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? iva { get; set; }

        [MaxLength(20)]
        public string? tb_interes_normal { get; set; }

        [MaxLength(20)]
        public string? tipo_tasa { get; set; }

        [MaxLength(20)]
        public string? tb_interes_moratorio { get; set; }

        [MaxLength(20)]
        public string? tipo_tasa_moratorio { get; set; }

        [MaxLength(200)]
        public string? destino { get; set; }

        // ── Comisión y seguro ──
        [Column(TypeName = "decimal(12,2)")]
        public decimal? comision_apertura { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? seguro_credito { get; set; }

        // ── Características financieras – Ingresos ──
        [Column(TypeName = "decimal(12,2)")]
        public decimal? ing_semanal { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? ing_otros { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? ing_total { get; set; }

        // ── Gastos ──
        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_alimento { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_luz { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_telefono { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_transporte { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_renta { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_inversion { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_creditos { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? g_otros { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? total_gasto { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? total_disponible { get; set; }

        // ── Cuenta desembolso ──
        [MaxLength(200)]
        public string? nombre_banco { get; set; }

        [MaxLength(100)]
        public string? num_cuenta { get; set; }

        // MONEYPINE-FIX: sistema fiscal que administra el crédito (SINF/SINB/COFI)
        [MaxLength(10)]
        public string? administrado_en { get; set; } = "SINF";

        // Grupo (prestamo grupal)
        public int? grupo_id { get; set; }

        [ForeignKey("grupo_id")]
        [JsonIgnore]
        public Grupo? Grupo { get; set; }

        // Relaciones
        [JsonIgnore]
        public Cliente Cliente { get; set; } = null!;

        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }
}