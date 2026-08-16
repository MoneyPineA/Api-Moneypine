using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("notificacion_agendada")]
    public class NotificacionAgendada : ITenantEntity
    {
        [Key]
        public int notificacion_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        [Required]
        public int prestamo_id { get; set; }

        [Required]
        [MaxLength(200)]
        public string titulo { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? detalles { get; set; }

        [Required]
        public DateTime fecha_hora { get; set; }

        public DateTime fecha_registro { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        [ForeignKey("prestamo_id")]
        public Prestamo Prestamo { get; set; } = null!;
    }
}
