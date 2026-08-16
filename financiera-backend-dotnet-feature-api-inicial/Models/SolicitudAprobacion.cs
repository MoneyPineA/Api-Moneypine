using ApiEjemplo.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    /// <summary>
    /// Peticion de un rol no-ADMIN para ejecutar una accion que afecta
    /// calculos de negocio (eliminar un pago, condonar mora sobre el tope,
    /// sacar a alguien del buro...).
    ///
    /// Es una sola tabla para todos los tipos a proposito: el dia que haya que
    /// pedir autorizacion para algo nuevo basta con agregar un valor a
    /// TipoSolicitud, sin migracion ni bandeja aparte.
    ///
    /// La accion NO se ejecuta al crear la solicitud. Se guarda la intencion y
    /// se aplica cuando el ADMIN aprueba, revalidando antes que el estado siga
    /// siendo el mismo que el solicitante describio.
    /// </summary>
    public class SolicitudAprobacion : ITenantEntity
    {
        public int id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        public TipoSolicitud tipo { get; set; }

        public EstadoSolicitud estado { get; set; } = EstadoSolicitud.PENDIENTE;

        // ── Quien pide ────────────────────────────────────────────────
        public int solicitante_id { get; set; }

        [ForeignKey(nameof(solicitante_id))]
        public Usuario? Solicitante { get; set; }

        /// <summary>Por que lo pide. Obligatorio: sin contexto el admin no puede decidir.</summary>
        public string justificacion { get; set; } = string.Empty;

        // ── Sobre que ─────────────────────────────────────────────────
        /// <summary>Id del pago / credito / cliente segun el tipo.</summary>
        public int entidad_id { get; set; }

        /// <summary>Cliente afectado, para mostrar contexto sin resolver joins en la UI.</summary>
        public int? cliente_id { get; set; }

        /// <summary>
        /// Monto involucrado al momento de solicitar. Sirve para mostrarlo en la
        /// bandeja y para detectar en la aprobacion si la cifra cambio.
        /// </summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal monto { get; set; }

        /// <summary>
        /// Datos extra que hagan falta para ejecutar la accion, en JSON.
        /// Se guarda como texto libre para que cada tipo de solicitud lleve lo
        /// suyo sin cambiar el esquema.
        /// </summary>
        public string? payload { get; set; }

        /// <summary>Resumen legible de lo que se pide, congelado al crear la solicitud.</summary>
        public string? descripcion { get; set; }

        // ── Como se resolvio ──────────────────────────────────────────
        public int? resuelta_por { get; set; }

        [ForeignKey(nameof(resuelta_por))]
        public Usuario? Resolutor { get; set; }

        /// <summary>
        /// Texto del ADMIN. Obligatorio al rechazar — el solicitante tiene que
        /// entender por que se le nego.
        /// </summary>
        public string? respuesta { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public DateTime? resuelta_at { get; set; }
    }
}
