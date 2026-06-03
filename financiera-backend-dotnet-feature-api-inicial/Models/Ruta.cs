using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ApiEjemplo.Models
{
    [Table("ruta")]
    public class Ruta
    {
        [Key]
        public int ruta_id { get; set; }

        [Required, MaxLength(30)]
        public string codigo { get; set; } = "";

        [Required, MaxLength(200)]
        public string nombre { get; set; } = "";

        public int gerencia_id { get; set; }

        [ForeignKey("gerencia_id")]
        [JsonIgnore]
        public Gerencia Gerencia { get; set; } = null!;

        public int? asesor_id { get; set; }

        [ForeignKey("asesor_id")]
        [JsonIgnore]
        public Usuario? Asesor { get; set; }
    }
}
