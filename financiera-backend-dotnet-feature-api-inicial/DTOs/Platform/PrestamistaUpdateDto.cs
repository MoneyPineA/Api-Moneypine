namespace ApiEjemplo.DTOs.Platform;

// MONEYPINE-MT: Fase 3 — PUT api/platform/prestamistas/{id}. Edita los datos
// administrables de un prestamista ya dado de alta.
//
// `slug` se declara aquí SOLO para poder detectarlo y rechazarlo con un
// mensaje explícito si viene en el body — nunca se asigna a la entidad.
// Es el identificador estable del tenant (usado en URLs, config, y cualquier
// referencia futura); permitir editarlo rompería esas referencias.
public class PrestamistaUpdateDto
{
    public string? slug { get; set; }

    public string nombre_comercial { get; set; } = null!;
    public string? razon_social { get; set; }
    public string? rfc { get; set; }
    public string? plan { get; set; }
    public string? moneda { get; set; }
    public string? zona_horaria { get; set; }

    // Datos de contacto del cliente de negocio (Fase 3)
    public string? correo_contacto { get; set; }
    public string? telefono_contacto { get; set; }
    public string? persona_contacto { get; set; }
    public string? direccion { get; set; }
    public string? ciudad { get; set; }
    public string? estado { get; set; } // entidad federativa — no confundir con `estatus`
    public string? notas { get; set; }
}
