using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    [Table("cuenta_ahorro")]
    public class CuentaAhorro
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int cliente_id { get; set; }

        [Required]
        public int producto_ahorro_id { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto_inicial { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal saldo_actual { get; set; }

        [Required]
        public DateOnly fecha_apertura { get; set; }

        [Required]
        public DateOnly fecha_vencimiento { get; set; }

        [Required]
        public EstatusAhorro estatus { get; set; } = EstatusAhorro.ACTIVA;

        // Ultimo dia YA capitalizado. Hace idempotente el motor de rendimiento:
        // solo se abonan los dias entre esta fecha y hoy, asi que ejecutarlo dos
        // veces el mismo dia no paga doble. Null = nunca se ha capitalizado
        // (se toma fecha_apertura como punto de partida).
        public DateOnly? fecha_ultimo_rendimiento { get; set; }

        // Total historico abonado por rendimientos (para reportes y para
        // saber cuanto de un retiro es capital y cuanto es ganancia).
        [Column(TypeName = "decimal(12,2)")]
        public decimal rendimiento_acumulado { get; set; } = 0;

        public int? ejecutivo_id { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navegación
        public Cliente Cliente { get; set; } = null!;
        public ProductoAhorro Producto { get; set; } = null!;

        [JsonIgnore]
        public ICollection<MovimientoAhorro> Movimientos { get; set; } = new List<MovimientoAhorro>();
    }
}
