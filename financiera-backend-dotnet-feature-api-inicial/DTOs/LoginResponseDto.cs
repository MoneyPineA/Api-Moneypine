namespace ApiEjemplo.DTOs
{
    public class LoginResponseDto
    {
        public string token { get; set; } = null!;
        public DateTime expira { get; set; }
    }
}