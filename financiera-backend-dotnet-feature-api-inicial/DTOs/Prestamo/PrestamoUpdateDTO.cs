using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Prestamo
{
    public class PrestamoUpdateDTO
    {
        // ── Campos base ──
        public decimal monto { get; set; }
        public int plazo_meses { get; set; }
        public EstatusPrestamo estatus { get; set; }
        public FormasPago forma_pago { get; set; } = FormasPago.MENSUAL;
        public DateTime? fecha_creacion { get; set; }
        // MONEYPINE-FIX: fecha primer pago — editable independiente de fecha_creacion
        public DateTime? fecha_inicio { get; set; }

        // ── Ruta vinculada (actualiza el Cliente) ──
        public string? ruta_vinculacion { get; set; }

        // ── Comisión y seguro ──
        public decimal? comision_apertura { get; set; }
        public decimal? seguro_credito { get; set; }

        // ── Ingresos ──
        public decimal? ing_semanal { get; set; }
        public decimal? ing_otros { get; set; }
        public decimal? ing_total { get; set; }

        // ── Gastos ──
        public decimal? g_alimento { get; set; }
        public decimal? g_luz { get; set; }
        public decimal? g_telefono { get; set; }
        public decimal? g_transporte { get; set; }
        public decimal? g_renta { get; set; }
        public decimal? g_inversion { get; set; }
        public decimal? g_creditos { get; set; }
        public decimal? g_otros { get; set; }
        public decimal? total_gasto { get; set; }
        public decimal? total_disponible { get; set; }

        // ── Cuenta desembolso ──
        public string? nombre_banco { get; set; }
        public string? num_cuenta { get; set; }

        // MONEYPINE-FIX: sistema fiscal que administra el crédito (SINF/SINB/COFI)
        public string? administrado_en { get; set; }
    }
}