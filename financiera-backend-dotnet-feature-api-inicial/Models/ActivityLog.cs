using ApiEjemplo.Enums;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    public class ActivityLog : ITenantEntity
    {
        public int Id { get; set; }

        // MONEYPINE-MT: tenant propietario del log (Fase 1 — Parte 4.3)
        public int prestamista_id { get; set; }

        public ActivityType Type { get; set; }

        public int ClientId { get; set; }

        public decimal Amount { get; set; }

        public NotificationLevel Priority { get; set; }

        public string? Description { get; set; }

        public int? UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}