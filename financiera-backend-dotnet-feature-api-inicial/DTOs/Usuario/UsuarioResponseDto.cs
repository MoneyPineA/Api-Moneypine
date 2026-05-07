namespace ApiEjemplo.DTOs.Usuario;
public class UsuarioResponseDto
{
    public int usuario_id { get; set; }
    public string correo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string apellido { get; set; } = null!;
    public string? telefono { get; set; }
    public string rol { get; set; } = null!;
    public string estado { get; set; } = null!;
    public DateTime fecha_registro { get; set; }
}