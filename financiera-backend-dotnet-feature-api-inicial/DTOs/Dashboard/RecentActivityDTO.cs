using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Dashboard
{
    public class RecentActivityDTO
    {
        public string Type { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int ClientId { get; set; }

        public decimal Amount { get; set; }

        public NotificationLevel Priority { get; set; }

        public string Color { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // MONEYPINE-FIX: prestamo_id extraído del Description para que el frontend muestre Ref. #prestamo, no #cliente
        public string? PrestamoId { get; set; }
    }
}