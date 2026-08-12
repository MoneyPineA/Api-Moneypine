using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Models
{
    [Table("cliente")]
    public class Cliente : ITenantEntity
    {
        [Key]
        public int cliente_id { get; set; }

        // MONEYPINE-MT: Fase 1 — Parte 4.3
        public int prestamista_id { get; set; }

        public string? clave_cliente { get; set; }

        //Llave foránea (OBLIGATORIA)
        [Required]
        public int usuario_id { get; set; }

        public string? tipo_cliente { get; set; }
        public string? ruta_vinculacion { get; set; }
        public bool permitir_acceso_web { get; set; } = false;

        public string? apellido_materno { get; set; }
        // MONEYPINE-FIX: apellido paterno separado — existe en la tabla Railway (varchar 20)
        public string? apellido_paterno { get; set; }
        // MONEYPINE-FIX: ciudad — columna agregada a Railway 2026-05-26
        public string? ciudad { get; set; }
        public string? sexo { get; set; }
        public string? estado_civil { get; set; }
        public string? lugar_nacimiento { get; set; }
        public int? no_dependientes { get; set; }
        public string? telefono_oficina { get; set; }
        public string? telefono_particular { get; set; }

        public string? direccion { get; set; }
        public string? colonia { get; set; }
        public string? cp { get; set; }
        public string? estado_domicilio { get; set; }
        public string? municipio { get; set; }
        public string? num_ext { get; set; }
        // MONEYPINE-FIX: calle separada — columna agregada a Railway via ALTER TABLE
        public string? calle { get; set; }
        // MONEYPINE-FIX: columnas que existen en Railway (ALTER TABLE del sistema anterior)
        // pero faltaban en el modelo — la migración que las acompaña NO las crea (ver AddClienteAnotacion)
        public string? numero_int { get; set; }
        public string? ref_calle1 { get; set; }
        public string? ref_calle2 { get; set; }
        public string? ref_adicional { get; set; }
        public string? tel_celular { get; set; }
        public DateTime? fec_alta { get; set; }
        public DateTime? fec_baja { get; set; }
        public double? latitud { get; set; }
        public double? longitud { get; set; }
        public DateTime? fecha_nacimiento { get; set; }
        public string? curp { get; set; }
        public string? rfc { get; set; }

        public string? empresa_nombre { get; set; }
        public string? empresa_rfc { get; set; }
        public string? empresa_correo { get; set; }
        public string? empresa_telefono_oficina { get; set; }
        public string? empresa_telefono_particular { get; set; }
        public string? empresa_telefono_celular { get; set; }

        //Relaciones (NO se envían en el POST)
        [JsonIgnore]
        public Usuario? Usuario { get; set; }

        [JsonIgnore]
        public ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();
    }
}