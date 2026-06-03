using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ApiEjemplo.Models
{
    [Table("movimiento_ahorro")]
    public class MovimientoAhorro
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int cuenta_ahorro_id { get; set; }

        // DEPOSITO | RENDIMIENTO | RETIRO
        [Required]
        [MaxLength(20)]
        public string tipo { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto { get; set; }

        [MaxLength(300)]
        public string? descripcion { get; set; }

        [Required]
        public DateOnly fecha { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public CuentaAhorro Cuenta { get; set; } = null!;
    }
}
