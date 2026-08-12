using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models;

[Table("prestamo_aval")]
public class PrestamoAval : ITenantEntity
{
    [Key]
    public int id { get; set; }

    // MONEYPINE-MT: Fase 1 — Parte 4.3
    public int prestamista_id { get; set; }

    [Required]
    public int prestamo_id { get; set; }

    [Required]
    public int cliente_id_aval { get; set; }

    [ForeignKey("prestamo_id")]
    [JsonIgnore]
    public Prestamo? Prestamo { get; set; }

    [ForeignKey("cliente_id_aval")]
    [JsonIgnore]
    public Cliente? Aval { get; set; }
}
