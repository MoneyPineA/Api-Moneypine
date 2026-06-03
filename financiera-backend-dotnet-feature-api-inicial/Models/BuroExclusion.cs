using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiEjemplo.Models
{
    [Table("buro_exclusion")]
    public class BuroExclusion
    {
        [Key]
        [Column("cliente_id")]
        public int cliente_id { get; set; }

        [Column("excluido_por")]
        public int? excluido_por { get; set; }

        [Column("fecha")]
        public DateTime fecha { get; set; } = DateTime.UtcNow;

        [Column("motivo")]
        [MaxLength(300)]
        public string motivo { get; set; } = "Excluido por administrador";
    }
}
