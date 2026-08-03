using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    [Table("producto_ahorro")]
    public class ProductoAhorro
    {
        [Key]
        public int id { get; set; }

        [Required]
        [MaxLength(100)]
        public string nombre { get; set; } = string.Empty;

        [Column(TypeName = "decimal(8,2)")]
        public decimal tasa_anual { get; set; } = 0;

        public int plazo_dias { get; set; } = 365;

        // VISTA: el cliente retira cuando quiera.
        // PLAZO_FIJO: bloqueado hasta fecha_vencimiento (plazo_dias desde la
        // apertura); solo un ADMIN puede autorizar el retiro anticipado.
        public TipoProductoAhorro tipo { get; set; } = TipoProductoAhorro.VISTA;

        [MaxLength(300)]
        public string? descripcion { get; set; }

        public bool activo { get; set; } = true;

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<CuentaAhorro> Cuentas { get; set; } = new List<CuentaAhorro>();
    }
}
