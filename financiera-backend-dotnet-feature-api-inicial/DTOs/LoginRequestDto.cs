using System.ComponentModel.DataAnnotations;

namespace ApiEjemplo.DTOs
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "El correo es obligatorio")]
        public string correo { get; set; } = null!;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        public string password { get; set; } = null!;
    }
}