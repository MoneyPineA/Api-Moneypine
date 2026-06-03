using System.ComponentModel.DataAnnotations;

namespace ApiEjemplo.DTOs.Cliente
{
    public class ClienteCreateDTO
    {
        [Required]
        public int usuario_id { get; set; }

        public string? direccion { get; set; }
        public DateTime? fecha_nacimiento { get; set; }
        public string? curp { get; set; }
        public string? rfc { get; set; }
    }
}