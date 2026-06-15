using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Usuario;
public class UsuarioUpdateDto
{
    public string correo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string apellido { get; set; } = null!;
    public string? telefono { get; set; }
    public RolUsuario rol { get; set; }
    public EstadoUsuario estado { get; set; }
    // Si viene null o vacío, no se toca password_hash
    public string? nuevaPassword { get; set; }
}