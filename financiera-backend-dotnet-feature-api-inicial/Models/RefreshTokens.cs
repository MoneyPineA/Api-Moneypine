using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiEjemplo.Models
{
    [Table("refresh_tokens")]
    public class RefreshToken
    {
        [Key]
        [Column("refresh_token_id")]
        public int RefreshTokenId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public Usuario Usuario { get; set; } = null!;

        [Column("token")]
        public string Token { get; set; } = null!;

        [Column("fecha_expiracion")]
        public DateTime ExpirationDate { get; set; }

        [Column("is_revoked")]
        public bool IsRevoked { get; set; }

        [Column("fecha_creacion")]
        public DateTime CreatedAt { get; set; }

        [Column("fecha_revocacion")]
        public DateTime? RevokedAt { get; set; }
    }
}