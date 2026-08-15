using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Models
{
    // MONEYPINE-MT: tabla de tenants. Documento de arquitectura, Parte 4.2.
    [Table("prestamista")]
    public class Prestamista
    {
        [Key]
        public int prestamista_id { get; set; }

        [Required, MaxLength(40)]
        public string slug { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string nombre_comercial { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? razon_social { get; set; }

        [MaxLength(20)]
        public string? rfc { get; set; }

        [Required]
        public EstatusPrestamista estatus { get; set; } = EstatusPrestamista.ACTIVO;

        [Required, MaxLength(30)]
        public string plan { get; set; } = "MVP";

        [Required, MaxLength(3)]
        public string moneda { get; set; } = "MXN";

        [Required, MaxLength(50)]
        public string zona_horaria { get; set; } = "America/Mexico_City";

        [MaxLength(300)]
        public string? logo_url { get; set; }

        [MaxLength(7)]
        public string? color_primario { get; set; }

        // Válvula de escape para preferencias por tenant (días de gracia, tasa
        // moratoria tope, si usa ahorro, si reporta a buró) sin migración nueva.
        public string? config_json { get; set; }

        [Required]
        public DateTime fecha_alta { get; set; }

        public DateTime? fecha_baja { get; set; }

        // MONEYPINE-MT: Fase 3 — datos con los que de verdad se administra un
        // cliente de negocio (dueño del prestamista), no del tenant técnico.
        [MaxLength(255)]
        public string? correo_contacto { get; set; }

        [MaxLength(30)]
        public string? telefono_contacto { get; set; }

        [MaxLength(150)]
        public string? persona_contacto { get; set; }

        [MaxLength(300)]
        public string? direccion { get; set; }

        [MaxLength(100)]
        public string? ciudad { get; set; }

        // MONEYPINE-MT: entidad federativa del domicilio del prestamista. Ojo:
        // NO confundir con `estatus` (arriba) — ese es ACTIVO/SUSPENDIDO/CANCELADO
        // del tenant. `estado` aquí es "Jalisco", "CDMX", etc.
        [MaxLength(100)]
        public string? estado { get; set; }

        // Notas libres del dueño de la plataforma sobre este cliente de negocio.
        // Sin MaxLength -> Pomelo lo mapea a LONGTEXT, igual que config_json.
        public string? notas { get; set; }
    }
}
