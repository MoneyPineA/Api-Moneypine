namespace ApiEjemplo.DTOs.Platform;

// MONEYPINE-MT: Fase 3 — respuesta del alta. La contraseña temporal viaja UNA
// sola vez, aquí; no se puede recuperar después (solo se guarda el hash).
public class PrestamistaCreateResponseDto
{
    public int prestamista_id { get; set; }
    public string slug { get; set; } = null!;
    public string nombre_comercial { get; set; } = null!;
    public string estatus { get; set; } = null!;
    public DateTime fecha_alta { get; set; }

    public int admin_usuario_id { get; set; }
    public string admin_correo { get; set; } = null!;
    public string admin_password_temporal { get; set; } = null!;
}
