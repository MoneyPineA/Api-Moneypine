using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    // MONEYPINE-MT: tabla de tenants. Documento de arquitectura, Parte 4.2.
    [Table("prestamista")]
    public class Prestamista
    {
        [Key]
        public int prestamista_id { get; set; }

        [Required, MaxLength(40)]
        public string slug { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string nombre_comercial { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? razon_social { get; set; }

        [MaxLength(20)]
        public string? rfc { get; set; }

        [Required]
        public EstatusPrestamista estatus { get; set; } = EstatusPrestamista.ACTIVO;

        [Required, MaxLength(30)]
        public string plan { get; set; } = "MVP";

        [Required, MaxLength(3)]
        public string moneda { get; set; } = "MXN";

        [Required, MaxLength(50)]
        public string zona_horaria { get; set; } = "America/Mexico_City";

        [MaxLength(300)]
        public string? logo_url { get; set; }

        [MaxLength(7)]
        public string? color_primario { get; set; }

        // Válvula de escape para preferencias por tenant (días de gracia, tasa
        // moratoria tope, si usa ahorro, si reporta a buró) sin migración nueva.
        public string? config_json { get; set; }

        [Required]
        public DateTime fecha_alta { get; set; }

        public DateTime? fecha_baja { get; set; }
    }
}
