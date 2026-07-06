using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiEjemplo.Models
{
    [Table("lista_negra")]
    public class ListaNegra
    {
        [Key]
        [Column("lista_negra_id")]
        public int lista_negra_id { get; set; }

        [Required]
        [Column("cliente_id")]
        public int cliente_id { get; set; }

        [Column("prestamo_id")]
        public int? prestamo_id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("motivo")]
        public string motivo { get; set; } = string.Empty;

        [Required]
        [Column("dias_mora")]
        public int dias_mora { get; set; }

        [Required]
        [Column("monto_mora", TypeName = "decimal(18,2)")]
        public decimal monto_mora { get; set; }

        // ACTIVO | REMOVIDO
        [Required]
        [MaxLength(20)]
        [Column("estado")]
        public string estado { get; set; } = "ACTIVO";

        // AUTOMATICO | MANUAL
        [Required]
        [MaxLength(50)]
        [Column("origen")]
        public string origen { get; set; } = "AUTOMATICO";

        [Required]
        [Column("fecha_alta")]
        public DateTime fecha_alta { get; set; } = DateTime.UtcNow;

        [Column("fecha_baja")]
        public DateTime? fecha_baja { get; set; }

        [Column("creado_por")]
        public int? creado_por { get; set; }

        [Column("actualizado_por")]
        public int? actualizado_por { get; set; }

        [MaxLength(500)]
        [Column("observaciones")]
        public string? observaciones { get; set; }

        [Required]
        [Column("fecha_creacion")]
        public DateTime fecha_creacion { get; set; } = DateTime.UtcNow;

        [Column("fecha_actualizacion")]
        public DateTime? fecha_actualizacion { get; set; }

        // Nav
        [ForeignKey("cliente_id")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("prestamo_id")]
        public Prestamo? Prestamo { get; set; }
    }
}
