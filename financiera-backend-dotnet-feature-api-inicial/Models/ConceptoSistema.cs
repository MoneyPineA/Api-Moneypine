using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiEjemplo.Models
{
    [Table("concepto_sistema")]
    public class ConceptoSistema
    {
        [Key]
        [Column("id")]
        public int id { get; set; }

        [Column("nombre")]
        [MaxLength(100)]
        public string nombre { get; set; } = "";

        // GASTO o INGRESO
        [Column("tipo")]
        [MaxLength(20)]
        public string tipo { get; set; } = "GASTO";

        [Column("activo")]
        public bool activo { get; set; } = true;

        [Column("orden")]
        public int orden { get; set; } = 0;

        [Column("created_at")]
        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}
