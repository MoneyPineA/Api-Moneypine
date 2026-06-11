public class PagoCreateDTO
{
    public int prestamo_id { get; set; }
    public decimal monto_pagado { get; set; }
    public DateTime? fecha_pago { get; set; }
    public string? metodo_pago { get; set; }
    public int? cobrador_id { get; set; }
    public string tipo_pago { get; set; } = "parcialidad_mora";
}
