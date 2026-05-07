namespace ApiEjemplo.DTOs.Dashboard
{
    public class DashboardFinancialSummaryDTO
    {
        public decimal cartera_total { get; set; }
        public decimal capital_actual { get; set; }
        public decimal interes_total { get; set; }
        public int numero_total_creditos { get; set; }
    }
}