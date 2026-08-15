using ApiEjemplo.Enums;

namespace ApiEjemplo.DTOs.Platform;

// MONEYPINE-MT: Fase 3 — fila de GET /api/platform/prestamistas/{id}/usuarios.
// Nunca incluye password_hash.
public class PrestamistaUsuarioDto
{
    public int usuario_id { get; set; }
    public string correo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string apellido { get; set; } = null!;
    public string? telefono { get; set; }
    public string rol { get; set; } = null!;
    public string estado { get; set; } = null!;
    public DateTime fecha_registro { get; set; }
    public DateTime? ultima_actividad { get; set; }
}

// PATCH api/platform/prestamistas/{id}/usuarios/{usuarioId}
// Patch parcial: solo se tocan los campos que vengan no-nulos en el body.
public class PrestamistaUsuarioUpdateDto
{
    public string? correo { get; set; }
    public string? nombre { get; set; }
    public string? apellido { get; set; }
    public string? telefono { get; set; }

    // MONEYPINE-MT: nunca se acepta PLATFORM_ADMIN aquí (se valida en el
    // controller) — crearía un administrador de plataforma dentro de un tenant.
    public RolUsuario? rol { get; set; }
}

// PATCH api/platform/prestamistas/{id}/usuarios/{usuarioId}/estado
public class PrestamistaUsuarioEstadoUpdateDto
{
    public EstadoUsuario estado { get; set; } // ACTIVO | INACTIVO | BLOQUEADO
}

// POST api/platform/prestamistas/{id}/usuarios/{usuarioId}/reset-password
// La contraseña temporal viaja UNA sola vez, aquí; no se puede recuperar después.
public class ResetPasswordResponseDto
{
    public int usuario_id { get; set; }
    public string correo { get; set; } = null!;
    public string password_temporal { get; set; } = null!;
}
