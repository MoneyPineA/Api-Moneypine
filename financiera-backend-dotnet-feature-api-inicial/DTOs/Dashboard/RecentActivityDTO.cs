using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Dashboard
{
    public class RecentActivityDTO
    {
        /// <summary>
        /// Id del ActivityLog. Sin el, el frontend solo tenia ClientId para
        /// identificar cada fila y un mismo cliente puede aparecer varias veces:
        /// React acababa con dos elementos usando la misma key.
        /// </summary>
        public int Id { get; set; }

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