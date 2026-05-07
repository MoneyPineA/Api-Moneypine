using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    [Table("grupo")]
    public class Grupo
    {
        [Key]
        public int grupo_id { get; set; }

        [Required]
        [MaxLength(150)]
        public string nombre { get; set; } = string.Empty;

        public DateTime fecha_creacion { get; set; } = DateTime.UtcNow;

        [MaxLength(20)]
        public string estatus { get; set; } = "ACTIVO";

        [MaxLength(100)]
        public string? clasificacion { get; set; }

        // Condiciones financieras (aplican a todos los miembros)
        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal tasa_interes { get; set; }

        [Required]
        public int plazo_meses { get; set; }

        [Required]
        public FormasPago forma_pago { get; set; } = FormasPago.DIARIA;

        // Campos CNBV
        [MaxLength(50)]
        public string? tipo_cnbv { get; set; }

        [MaxLength(20)]
        public string? tb_interes_normal { get; set; }

        [MaxLength(20)]
        public string? tipo_tasa { get; set; }

        [MaxLength(20)]
        public string? tb_interes_moratorio { get; set; }

        [MaxLength(20)]
        public string? tipo_tasa_moratorio { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? moratorio_por_dia { get; set; }

        [MaxLength(200)]
        public string? destino { get; set; }

        // Navegación
        public ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();
    }
}
