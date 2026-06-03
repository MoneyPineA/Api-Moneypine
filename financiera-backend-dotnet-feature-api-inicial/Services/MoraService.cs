namespace ApiEjemplo.Services
{
    public class MoraService
    {
        private const decimal TasaMensual = 0.02m;
        //aumento diario aprox. 0.067%
        public decimal CalcularMora(decimal monto, int diasAtraso)
        {
            if (diasAtraso <= 0)
                return 0;

            decimal tasaDiaria = TasaMensual / 30;
            decimal mora = monto * tasaDiaria * diasAtraso;

            return Math.Round(mora, 2);
        }
    }
}