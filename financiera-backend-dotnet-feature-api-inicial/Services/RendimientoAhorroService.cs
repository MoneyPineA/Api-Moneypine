using ApiEjemplo.Enums;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    /// <summary>
    /// Motor de rendimientos de cuentas de ahorro.
    ///
    /// Modelo: INTERES COMPUESTO DIARIO (estilo Nu / Mercado Pago).
    /// Cada dia gana sobre el saldo del dia anterior, es decir, lo ya ganado
    /// tambien genera rendimiento:
    ///
    ///     saldo_final = saldo * (1 + tasa_anual/100/365) ^ dias
    ///
    /// La logica de calculo es estatica y pura (sin BD) para poder probarla
    /// con tests unitarios; el controlador se encarga de persistir.
    /// </summary>
    public class RendimientoAhorroService
    {
        public const int DiasDelAnio = 365;

        /// <summary>Tasa diaria equivalente a partir de la tasa anual (en %).</summary>
        public static decimal TasaDiaria(decimal tasaAnualPorcentaje)
            => tasaAnualPorcentaje / 100m / DiasDelAnio;

        /// <summary>
        /// Calcula el rendimiento compuesto generado por <paramref name="saldo"/>
        /// durante <paramref name="dias"/> dias. Devuelve SOLO la ganancia
        /// (no incluye el capital), redondeada a 2 decimales.
        /// </summary>
        public static decimal CalcularRendimiento(decimal saldo, decimal tasaAnualPorcentaje, int dias)
        {
            if (dias <= 0 || saldo <= 0 || tasaAnualPorcentaje <= 0)
                return 0m;

            // Se usa double para la potencia y se vuelve a decimal al final:
            // el redondeo a centavos absorbe la imprecision del punto flotante.
            var tasaDiaria = (double)TasaDiaria(tasaAnualPorcentaje);
            var factor     = Math.Pow(1 + tasaDiaria, dias);
            var saldoFinal = (decimal)((double)saldo * factor);

            return Math.Round(saldoFinal - saldo, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Dias pendientes de capitalizar para una cuenta: desde el ultimo dia
        /// ya abonado (o la apertura si nunca se abono) hasta <paramref name="hoy"/>.
        /// En PLAZO_FIJO no se paga mas alla del vencimiento.
        /// </summary>
        public static int DiasPendientes(CuentaAhorro cuenta, DateOnly hoy)
        {
            var desde = cuenta.fecha_ultimo_rendimiento ?? cuenta.fecha_apertura;

            // El plazo fijo deja de generar al vencer; el ahorro a la vista sigue.
            var hasta = cuenta.Producto?.tipo == TipoProductoAhorro.PLAZO_FIJO && hoy > cuenta.fecha_vencimiento
                ? cuenta.fecha_vencimiento
                : hoy;

            var dias = hasta.DayNumber - desde.DayNumber;
            return dias > 0 ? dias : 0;
        }

        /// <summary>
        /// Rendimiento pendiente de abonar a la fecha indicada.
        /// No modifica la cuenta: sirve para mostrar "llevas ganado $X".
        /// </summary>
        public static decimal RendimientoPendiente(CuentaAhorro cuenta, DateOnly hoy)
        {
            if (cuenta.estatus != EstatusAhorro.ACTIVA) return 0m;
            var tasa = cuenta.Producto?.tasa_anual ?? 0m;
            return CalcularRendimiento(cuenta.saldo_actual, tasa, DiasPendientes(cuenta, hoy));
        }

        /// <summary>
        /// Proyeccion de cuanto habra ganado la cuenta al llegar el vencimiento,
        /// asumiendo que no hay retiros ni depositos. Para mostrar al cliente
        /// "al vencimiento recibiras $X".
        /// </summary>
        public static decimal ProyeccionAlVencimiento(CuentaAhorro cuenta, DateOnly hoy)
        {
            if (cuenta.estatus != EstatusAhorro.ACTIVA) return 0m;
            var tasa = cuenta.Producto?.tasa_anual ?? 0m;

            var desde = cuenta.fecha_ultimo_rendimiento ?? cuenta.fecha_apertura;
            var dias  = cuenta.fecha_vencimiento.DayNumber - desde.DayNumber;
            if (dias <= 0) return 0m;

            return CalcularRendimiento(cuenta.saldo_actual, tasa, dias);
        }

        /// <summary>
        /// Indica si la cuenta permite retirar en la fecha dada sin autorizacion
        /// especial. Un PLAZO_FIJO no vencido requiere autorizacion de ADMIN.
        /// </summary>
        public static bool PermiteRetiroLibre(CuentaAhorro cuenta, DateOnly hoy)
        {
            if (cuenta.Producto?.tipo != TipoProductoAhorro.PLAZO_FIJO) return true;
            return hoy >= cuenta.fecha_vencimiento;
        }
    }
}
