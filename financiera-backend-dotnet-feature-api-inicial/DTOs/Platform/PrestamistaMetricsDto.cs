namespace ApiEjemplo.DTOs.Platform;

// MONEYPINE-MT: Fase 3 — fila de GET /api/platform/prestamistas. Solo agregados,
// nunca filas de cartera (el documento de arquitectura lo prohíbe para PLATFORM_ADMIN).
public class PrestamistaMetricsDto
{
    public int prestamista_id { get; set; }
    public string slug { get; set; } = null!;
    public string nombre_comercial { get; set; } = null!;
    public string estatus { get; set; } = null!;
    public string plan { get; set; } = null!;
    public DateTime fecha_alta { get; set; }

    public int total_trabajadores { get; set; }
    public int total_clientes { get; set; }
    public int total_creditos { get; set; }
    public int creditos_activos { get; set; }
    public decimal cartera_total { get; set; }
}

// GET /api/platform/prestamistas/{id} — mismo detalle + desglose de trabajadores por rol.
public class PrestamistaDetailDto : PrestamistaMetricsDto
{
    public string? razon_social { get; set; }
    public string? rfc { get; set; }
    public string moneda { get; set; } = null!;
    public string zona_horaria { get; set; } = null!;
    public List<TrabajadoresPorRolDto> trabajadores_por_rol { get; set; } = new();
}

public class TrabajadoresPorRolDto
{
    public string rol { get; set; } = null!;
    public int total { get; set; }
}

// GET /api/platform/resumen
public class ResumenPlataformaDto
{
    public int prestamistas_total { get; set; }
    public int prestamistas_activos { get; set; }
    public int prestamistas_suspendidos { get; set; }
    public int prestamistas_cancelados { get; set; }

    public int total_trabajadores { get; set; }
    public int total_clientes { get; set; }
    public int total_creditos { get; set; }
    public int creditos_activos { get; set; }
    public decimal cartera_total { get; set; }
}
