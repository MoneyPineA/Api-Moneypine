using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Models
{
    [Table("buro_auto_reporte")]
    [PrimaryKey(nameof(cliente_id), nameof(prestamo_id))]
    public class BuroAutoReporte
    {
        public int cliente_id { get; set; }
        public int prestamo_id { get; set; }
        public DateTime fecha_reporte { get; set; } = DateTime.UtcNow;
        public int dias_mora { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal saldo_pendiente { get; set; }

        [MaxLength(300)]
        public string motivo { get; set; } = "AUTO-REPORTADO por mora >= 90 dias";
    }
}
