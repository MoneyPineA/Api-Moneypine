using System.ComponentModel.DataAnnotations;
using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Grupo;

public class GrupoCreateDto
{
    [Required]
    public string nombre { get; set; } = string.Empty;

    [Required, MinLength(2, ErrorMessage = "Se requieren al menos 2 miembros")]
    public List<int> cliente_ids { get; set; } = new();

    [Required]
    public decimal monto { get; set; }

    public decimal tasa_interes { get; set; }

    [Required]
    public int plazo_meses { get; set; } = 1;

    public FormasPago forma_pago { get; set; } = FormasPago.DIARIA;

    public DateTime fecha_inicio { get; set; }

    public int dias_gracia { get; set; } = 0;

    public string? clasificacion { get; set; }
    public string? tipo_cnbv { get; set; }
    public string? tb_interes_normal { get; set; }
    public string? tipo_tasa { get; set; }
    public string? tb_interes_moratorio { get; set; }
    public string? tipo_tasa_moratorio { get; set; }
    public decimal? moratorio_por_dia { get; set; }
    public string? destino { get; set; }
}
