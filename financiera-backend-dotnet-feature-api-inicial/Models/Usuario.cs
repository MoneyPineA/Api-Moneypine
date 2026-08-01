using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    [Table("usuario")]
    public class Usuario
    {
        [Key]
        public int usuario_id { get; set; }

        [Required]
        [EmailAddress]
        public string correo { get; set; } = null!;

        [Required]
        public string password_hash { get; set; } = null!;

        [Required]
        public string nombre { get; set; } = null!;

        [Required]
        public string apellido { get; set; } = null!;

        public string? telefono { get; set; }

        [Required]
        public RolUsuario rol { get; set; }
        // CLIENTE | ADMIN | COBRADOR

        public EstadoUsuario estado { get; set; } = EstadoUsuario.ACTIVO;
        // ACTIVO | INACTIVO | BLOQUEADO
        
        public DateTime fecha_registro { get; set; } = DateTime.UtcNow;

        // MONEYPINE-FIX: presencia (Reportes/Trabajadores conectados) — actualizados por
        // PresenceTrackingMiddleware en cada request autenticado.
        public DateTime? ultima_actividad { get; set; }

        [MaxLength(255)]
        public string? ultimo_user_agent { get; set; }
    }
}