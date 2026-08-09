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

        /// <summary>
        /// Se marca cuando un ADMIN saca al cliente de la lista negra a mano.
        ///
        /// La sincronizacion automatica no vuelve a agregarlo aunque siga
        /// cumpliendo los criterios de mora: sacarlo fue una decision humana
        /// deliberada y un proceso de fondo no debe revertirla. Para que
        /// reingrese hace falta que otro ADMIN lo agregue manualmente, y ese
        /// alta si lo reporta a buro de credito.
        ///
        /// Sin esta bandera la sincronizacion recreaba la entrada minutos
        /// despues, dejando sin efecto la autorizacion del administrador.
        /// </summary>
        [Column("bloquea_reingreso_auto")]
        public bool bloquea_reingreso_auto { get; set; } = false;

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
