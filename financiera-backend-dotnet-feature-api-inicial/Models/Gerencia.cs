using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ApiEjemplo.Models
{
    [Table("gerencia")]
    public class Gerencia
    {
        [Key]
        public int gerencia_id { get; set; }

        [Required, MaxLength(100)]
        public string nombre { get; set; } = "";

        [Required, MaxLength(20)]
        public string codigo { get; set; } = "";

        public bool es_principal { get; set; } = false;

        public int? gerente_id { get; set; }

        [ForeignKey("gerente_id")]
        [JsonIgnore]
        public Usuario? Gerente { get; set; }

        [JsonIgnore]
        public ICollection<Ruta> Rutas { get; set; } = new List<Ruta>();
    }
}
