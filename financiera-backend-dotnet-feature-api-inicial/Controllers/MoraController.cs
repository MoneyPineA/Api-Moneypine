using ApiEjemplo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    private readonly MoraService _moraService;

    public PagosController(MoraService moraService)
    {
        _moraService = moraService;
    }

    [HttpGet("calcular-mora")]
    public IActionResult CalcularMora(
        decimal monto,
        int diasAtraso)
    {
        var mora = _moraService.CalcularMora(monto, diasAtraso);

        return Ok(new
        {
            Monto = monto,
            DiasAtraso = diasAtraso,
            Mora = mora,
            TotalAPagar = monto + mora
        });
    }
}