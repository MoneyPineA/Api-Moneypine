using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiEjemplo.Models
{
    [Table("buro_auto_reporte")]
    public class BuroAutoReporte
    {
        // PK compuesta (cliente_id, prestamo_id) — un cliente puede tener varios créditos reportados
        [Key, Column(Order = 0)]
        public int cliente_id { get; set; }
        [Key, Column(Order = 1)]
        public int prestamo_id { get; set; }
        public DateTime fecha_reporte { get; set; } = DateTime.UtcNow;
        public int dias_mora { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal saldo_pendiente { get; set; }

        [MaxLength(300)]
        public string motivo { get; set; } = "AUTO-REPORTADO por mora >= 90 dias";
    }
}
