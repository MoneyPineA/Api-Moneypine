using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        public ActivityType Type { get; set; }

        public int ClientId { get; set; }

        public decimal Amount { get; set; }

        public NotificationLevel Priority { get; set; }

        public string? Description { get; set; }

        public int? UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}