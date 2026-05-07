using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Usuario;
public class UsuarioCreateDto
{
    public string correo { get; set; } = null!;
    public string password { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string apellido { get; set; } = null!;
    public string? telefono { get; set; }
    public RolUsuario rol { get; set; }
}