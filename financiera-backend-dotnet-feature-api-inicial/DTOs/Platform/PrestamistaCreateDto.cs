namespace ApiEjemplo.DTOs.Platform;

// MONEYPINE-MT: Fase 3 — alta de un prestamista nuevo desde el panel de plataforma.
public class PrestamistaCreateDto
{
    public string slug { get; set; } = null!;
    public string nombre_comercial { get; set; } = null!;
    public string? razon_social { get; set; }
    public string? rfc { get; set; }
    public string? plan { get; set; }
    public string? moneda { get; set; }
    public string? zona_horaria { get; set; }

    // Usuario ADMIN inicial del tenant nuevo
    public string admin_correo { get; set; } = null!;
    public string admin_nombre { get; set; } = null!;
    public string admin_apellido { get; set; } = null!;
    public string? admin_telefono { get; set; }
}
