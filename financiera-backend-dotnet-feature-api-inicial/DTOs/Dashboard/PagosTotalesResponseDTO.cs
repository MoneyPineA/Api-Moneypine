namespace ApiEjemplo.DTOs.Dashboard
{
    public class PagosTotalesResponseDTO
    {
        public string period { get; set; } = "monthly";

        public List<PagosTotalesItemDTO> data { get; set; } = new();
    }

    public class PagosTotalesItemDTO
    {
        public string date { get; set; } = string.Empty;
        public decimal total { get; set; }
    }
}