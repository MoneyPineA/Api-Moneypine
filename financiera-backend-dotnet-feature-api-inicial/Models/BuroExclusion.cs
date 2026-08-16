using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("buro_exclusion")]
    public class BuroExclusion : ITenantEntity
    {
        [Key]
        [Column("cliente_id")]
        public int cliente_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3. Tabla creada por SQL crudo en
        // Program.cs; esta migración la adopta con CREATE TABLE IF NOT EXISTS.
        [Column("prestamista_id")]
        public int prestamista_id { get; set; }

        [Column("excluido_por")]
        public int? excluido_por { get; set; }

        [Column("fecha")]
        public DateTime fecha { get; set; } = DateTime.UtcNow;

        [Column("motivo")]
        [MaxLength(300)]
        public string motivo { get; set; } = "Excluido por administrador";
    }
}
