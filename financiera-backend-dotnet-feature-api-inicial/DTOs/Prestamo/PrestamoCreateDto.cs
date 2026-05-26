using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Prestamo;
public class PrestamoCreateDTO
{
    public int cliente_id { get; set; }
    public decimal monto { get; set; }
    public decimal tasa_interes { get; set; }
    public int plazo_meses { get; set; }
    public DateTime fecha_inicio { get; set; }
    public int dias_gracia { get; set; }
    public DateTime? fecha_creacion { get; set; }
    public FormasPago forma_pago { get; set; } = FormasPago.MENSUAL;
    public int? cobrador_id { get; set; }

    // Campos CNBV / producto
    public string? tipo_cnbv { get; set; }
    public decimal? iva { get; set; }
    public string? tb_interes_normal { get; set; }
    public string? tipo_tasa { get; set; }
    public string? tb_interes_moratorio { get; set; }
    public string? tipo_tasa_moratorio { get; set; }
    public decimal? moratorio_por_dia { get; set; }
    public string? destino { get; set; }

    // MONEYPINE-FIX: lista de cliente_id que actúan como avales del préstamo
    public List<int>? avales { get; set; }

    // MONEYPINE-FIX: sistema fiscal que administra el crédito (SINF/SINB/COFI)
    public string? administrado_en { get; set; }
}
