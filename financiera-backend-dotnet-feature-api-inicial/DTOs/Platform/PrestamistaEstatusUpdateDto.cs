namespace ApiEjemplo.DTOs.Platform;

// MONEYPINE-MT: Fase 3 — cambio de estatus de un prestamista.
// confirmar_tenant_1 solo se exige cuando id == 1 y estatus != ACTIVO: es el
// tenant de producción, suspenderlo o cancelarlo por error deja a todos fuera.
public class PrestamistaEstatusUpdateDto
{
    public string estatus { get; set; } = null!; // ACTIVO | SUSPENDIDO | CANCELADO
    public bool confirmar_tenant_1 { get; set; } = false;
}
