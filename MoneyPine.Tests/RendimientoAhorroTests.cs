using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using ApiEjemplo.Services;
using Xunit;

namespace MoneyPine.Tests;

/// <summary>
/// Pruebas del motor de rendimientos de ahorro.
/// Cubren el calculo compuesto diario, la idempotencia (no pagar dos veces
/// los mismos dias), el tope del plazo fijo al vencer y el candado de retiro.
/// </summary>
public class RendimientoAhorroTests
{
    // ── Helpers ────────────────────────────────────────────────────────────
    private static CuentaAhorro Cuenta(
        decimal saldo,
        decimal tasaAnual,
        TipoProductoAhorro tipo = TipoProductoAhorro.VISTA,
        int plazoDias = 365,
        DateOnly? apertura = null,
        DateOnly? ultimoRendimiento = null,
        EstatusAhorro estatus = EstatusAhorro.ACTIVA)
    {
        var ap = apertura ?? new DateOnly(2026, 1, 1);
        return new CuentaAhorro
        {
            id = 1,
            saldo_actual = saldo,
            monto_inicial = saldo,
            fecha_apertura = ap,
            fecha_vencimiento = ap.AddDays(plazoDias),
            fecha_ultimo_rendimiento = ultimoRendimiento,
            estatus = estatus,
            Producto = new ProductoAhorro { tasa_anual = tasaAnual, plazo_dias = plazoDias, tipo = tipo },
        };
    }

    // ── Calculo base ───────────────────────────────────────────────────────

    [Fact]
    public void SinDias_NoGeneraRendimiento()
    {
        Assert.Equal(0m, RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 0));
    }

    [Fact]
    public void TasaCero_NoGeneraRendimiento()
    {
        Assert.Equal(0m, RendimientoAhorroService.CalcularRendimiento(10_000m, 0m, 30));
    }

    [Fact]
    public void SaldoCero_NoGeneraRendimiento()
    {
        Assert.Equal(0m, RendimientoAhorroService.CalcularRendimiento(0m, 12m, 30));
    }

    [Fact]
    public void DiasNegativos_NoGeneraRendimiento()
    {
        // Defensa: una fecha de ultimo rendimiento futura no debe "cobrar" al cliente
        Assert.Equal(0m, RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, -5));
    }

    [Fact]
    public void UnDia_AplicaTasaDiaria()
    {
        // 10,000 al 12% anual => 10000 * 0.12/365 = 3.2876... => 3.29
        var r = RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 1);
        Assert.Equal(3.29m, r);
    }

    [Fact]
    public void UnAnioCompleto_RindeCercaDeLaTasaAnual()
    {
        // Compuesto diario al 12%: (1+0.12/365)^365 - 1 = 12.747% efectivo
        var r = RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 365);
        Assert.InRange(r, 1_270m, 1_280m);
    }

    [Fact]
    public void EsCompuesto_NoSimple()
    {
        // El compuesto debe superar al simple en el mismo periodo
        var compuesto = RendimientoAhorroService.CalcularRendimiento(100_000m, 12m, 365);
        var simple    = 100_000m * 0.12m;
        Assert.True(compuesto > simple,
            $"El compuesto ({compuesto}) deberia superar al simple ({simple})");
    }

    [Fact]
    public void ElRendimientoCreceConElTiempo()
    {
        var d30  = RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 30);
        var d60  = RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 60);
        var d90  = RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 90);
        Assert.True(d30 < d60 && d60 < d90);
    }

    [Fact]
    public void RedondeaADosDecimales()
    {
        var r = RendimientoAhorroService.CalcularRendimiento(12_345.67m, 9.5m, 17);
        Assert.Equal(Math.Round(r, 2), r);
    }

    // ── Dias pendientes / idempotencia ─────────────────────────────────────

    [Fact]
    public void CuentaNueva_CuentaDiasDesdeLaApertura()
    {
        var c = Cuenta(10_000m, 12m, apertura: new DateOnly(2026, 1, 1));
        Assert.Equal(10, RendimientoAhorroService.DiasPendientes(c, new DateOnly(2026, 1, 11)));
    }

    [Fact]
    public void YaCapitalizado_SoloCuentaLosDiasNuevos()
    {
        var c = Cuenta(10_000m, 12m,
            apertura: new DateOnly(2026, 1, 1),
            ultimoRendimiento: new DateOnly(2026, 1, 20));
        Assert.Equal(5, RendimientoAhorroService.DiasPendientes(c, new DateOnly(2026, 1, 25)));
    }

    [Fact]
    public void MismoDia_NoPagaDosVeces()
    {
        // Idempotencia: si ya se capitalizo hoy, no hay dias pendientes
        var hoy = new DateOnly(2026, 3, 10);
        var c = Cuenta(10_000m, 12m, apertura: new DateOnly(2026, 1, 1), ultimoRendimiento: hoy);

        Assert.Equal(0, RendimientoAhorroService.DiasPendientes(c, hoy));
        Assert.Equal(0m, RendimientoAhorroService.RendimientoPendiente(c, hoy));
    }

    [Fact]
    public void CuentaCancelada_NoGeneraRendimiento()
    {
        var c = Cuenta(10_000m, 12m, apertura: new DateOnly(2026, 1, 1), estatus: EstatusAhorro.CANCELADA);
        Assert.Equal(0m, RendimientoAhorroService.RendimientoPendiente(c, new DateOnly(2026, 6, 1)));
    }

    // ── Plazo fijo ─────────────────────────────────────────────────────────

    [Fact]
    public void PlazoFijo_DejaDeGenerarAlVencer()
    {
        // Plazo de 30 dias, consultado 100 dias despues: solo paga los 30
        var c = Cuenta(10_000m, 12m, TipoProductoAhorro.PLAZO_FIJO, plazoDias: 30,
                       apertura: new DateOnly(2026, 1, 1));
        Assert.Equal(30, RendimientoAhorroService.DiasPendientes(c, new DateOnly(2026, 4, 11)));
    }

    [Fact]
    public void Vista_SigueGenerandoDespuesDelPlazo()
    {
        // El ahorro a la vista no se detiene en fecha_vencimiento
        var c = Cuenta(10_000m, 12m, TipoProductoAhorro.VISTA, plazoDias: 30,
                       apertura: new DateOnly(2026, 1, 1));
        Assert.Equal(100, RendimientoAhorroService.DiasPendientes(c, new DateOnly(2026, 4, 11)));
    }

    // ── Candado de retiro ──────────────────────────────────────────────────

    [Fact]
    public void Vista_PermiteRetiroSiempre()
    {
        var c = Cuenta(10_000m, 6m, TipoProductoAhorro.VISTA, plazoDias: 365,
                       apertura: new DateOnly(2026, 1, 1));
        Assert.True(RendimientoAhorroService.PermiteRetiroLibre(c, new DateOnly(2026, 1, 2)));
    }

    [Fact]
    public void PlazoFijo_BloqueaAntesDelVencimiento()
    {
        var c = Cuenta(10_000m, 6m, TipoProductoAhorro.PLAZO_FIJO, plazoDias: 60,
                       apertura: new DateOnly(2026, 1, 1));
        Assert.False(RendimientoAhorroService.PermiteRetiroLibre(c, new DateOnly(2026, 2, 15)));
    }

    [Fact]
    public void PlazoFijo_PermiteElDiaDelVencimiento()
    {
        var c = Cuenta(10_000m, 6m, TipoProductoAhorro.PLAZO_FIJO, plazoDias: 60,
                       apertura: new DateOnly(2026, 1, 1));
        Assert.True(RendimientoAhorroService.PermiteRetiroLibre(c, new DateOnly(2026, 3, 2)));
    }

    // ── Proyeccion ─────────────────────────────────────────────────────────

    [Fact]
    public void Proyeccion_CalculaHastaElVencimiento()
    {
        // 100 dias de plazo, consultado el dia 1: proyecta los 100 dias
        var c = Cuenta(10_000m, 12m, TipoProductoAhorro.PLAZO_FIJO, plazoDias: 100,
                       apertura: new DateOnly(2026, 1, 1));
        var esperado = RendimientoAhorroService.CalcularRendimiento(10_000m, 12m, 100);
        Assert.Equal(esperado, RendimientoAhorroService.ProyeccionAlVencimiento(c, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Proyeccion_EsCeroSiYaVencio()
    {
        var c = Cuenta(10_000m, 12m, TipoProductoAhorro.PLAZO_FIJO, plazoDias: 30,
                       apertura: new DateOnly(2026, 1, 1),
                       ultimoRendimiento: new DateOnly(2026, 1, 31));
        Assert.Equal(0m, RendimientoAhorroService.ProyeccionAlVencimiento(c, new DateOnly(2026, 2, 10)));
    }

    // ── Escenario integral ─────────────────────────────────────────────────

    [Fact]
    public void Escenario_CapitalizarPorPartesEquivaleAlPeriodoCompleto()
    {
        // Capitalizar 30+30 dias debe dar practicamente lo mismo que 60 de una vez.
        // Se admite 1 centavo de diferencia por el redondeo intermedio.
        const decimal saldo = 50_000m, tasa = 10m;

        var deUnaVez = RendimientoAhorroService.CalcularRendimiento(saldo, tasa, 60);

        var primer = RendimientoAhorroService.CalcularRendimiento(saldo, tasa, 30);
        var segundo = RendimientoAhorroService.CalcularRendimiento(saldo + primer, tasa, 30);
        var porPartes = primer + segundo;

        Assert.True(Math.Abs(deUnaVez - porPartes) <= 0.01m,
            $"De una vez: {deUnaVez}, por partes: {porPartes}");
    }
}
