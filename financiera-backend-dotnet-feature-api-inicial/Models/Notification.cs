using ApiEjemplo.Enums;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    public class Notification : ITenantEntity
    {
        public int Id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        public int UsuarioId { get; set; }

        public string Message { get; set; } = string.Empty;

        public NotificationLevel Level { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Color
        {
            get
            {
                return Level switch
                {
                    NotificationLevel.HIGH => "red",
                    NotificationLevel.NEUTRAL => "yellow",
                    NotificationLevel.POSITIVE => "green",
                    _ => "gray"
                };
            }
        }
    }
}