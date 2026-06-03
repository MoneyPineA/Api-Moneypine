using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using ApiEjemplo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AhorroController : ControllerBase
    {
        private readonly AppDbContext    _context;
        private readonly ActivityService _activity;

        public AhorroController(AppDbContext context, ActivityService activity)
        {
            _context  = context;
            _activity = activity;
        }

        // =====================================================
        // GET /api/Ahorro/productos
        // Lista todos los productos de ahorro activos
        // =====================================================
        [HttpGet("productos")]
        public async Task<IActionResult> GetProductos()
        {
            var productos = await _context.ProductosAhorro
                .Where(p => p.activo)
                .OrderBy(p => p.id)
                .ToListAsync();
            return Ok(productos);
        }

        // =====================================================
        // POST /api/Ahorro/productos
        // Crea un nuevo producto de ahorro
        // =====================================================
        [Authorize]
        [HttpPost("productos")]
        public async Task<IActionResult> CreateProducto([FromBody] ProductoAhorro dto)
        {
            dto.id         = 0;
            dto.activo     = true;
            dto.created_at = DateTime.UtcNow;
            _context.ProductosAhorro.Add(dto);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProductos), new { id = dto.id }, dto);
        }

        // =====================================================
        // PUT /api/Ahorro/productos/{id}
        // Actualiza nombre, tasa y plazo de un producto
        // =====================================================
        [Authorize]
        [HttpPut("productos/{id:int}")]
        public async Task<IActionResult> UpdateProducto(int id, [FromBody] ProductoAhorro dto)
        {
            var existing = await _context.ProductosAhorro.FindAsync(id);
            if (existing == null || !existing.activo) return NotFound("Producto no encontrado");

            existing.nombre      = dto.nombre;
            existing.tasa_anual  = dto.tasa_anual;
            existing.plazo_dias  = dto.plazo_dias;
            existing.descripcion = dto.descripcion;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        // =====================================================
        // DELETE /api/Ahorro/productos/{id}   (soft-delete)
        // =====================================================
        [Authorize]
        [HttpDelete("productos/{id:int}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            var existing = await _context.ProductosAhorro.FindAsync(id);
            if (existing == null) return NotFound("Producto no encontrado");

            existing.activo = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =====================================================
        // GET /api/Ahorro/cuentas
        // Todas las cuentas con cliente y producto
        // =====================================================
        [Authorize]
        [HttpGet("cuentas")]
        public async Task<IActionResult> GetCuentas()
        {
            var cuentas = await _context.CuentasAhorro
                .Include(c => c.Cliente)
                    .ThenInclude(cl => cl.Usuario)
                .Include(c => c.Producto)
                .OrderByDescending(c => c.created_at)
                .Select(c => new
                {
                    c.id,
                    c.cliente_id,
                    nombreCliente = (c.Cliente.Usuario.nombre ?? "") + " " + (c.Cliente.Usuario.apellido ?? ""),
                    c.producto_ahorro_id,
                    nombreProducto = c.Producto.nombre,
                    c.Producto.tasa_anual,
                    c.Producto.plazo_dias,
                    c.monto_inicial,
                    c.saldo_actual,
                    c.fecha_apertura,
                    c.fecha_vencimiento,
                    estatus = c.estatus.ToString(),
                    c.ejecutivo_id,
                    c.created_at,
                })
                .ToListAsync();
            return Ok(cuentas);
        }

        // =====================================================
        // GET /api/Ahorro/cuentas/activas
        // Solo cuentas ACTIVAS
        // =====================================================
        [Authorize]
        [HttpGet("cuentas/activas")]
        public async Task<IActionResult> GetCuentasActivas()
        {
            var cuentas = await _context.CuentasAhorro
                .Include(c => c.Cliente).ThenInclude(cl => cl.Usuario)
                .Include(c => c.Producto)
                .Where(c => c.estatus == EstatusAhorro.ACTIVA)
                .OrderByDescending(c => c.created_at)
                .Select(c => new
                {
                    c.id,
                    c.cliente_id,
                    nombreCliente = (c.Cliente.Usuario.nombre ?? "") + " " + (c.Cliente.Usuario.apellido ?? ""),
                    nombreProducto = c.Producto.nombre,
                    c.monto_inicial,
                    c.saldo_actual,
                    c.fecha_apertura,
                    c.fecha_vencimiento,
                    estatus = c.estatus.ToString(),
                })
                .ToListAsync();
            return Ok(cuentas);
        }

        // =====================================================
        // GET /api/Ahorro/cuentas/vencidas
        // Solo cuentas VENCIDAS
        // =====================================================
        [Authorize]
        [HttpGet("cuentas/vencidas")]
        public async Task<IActionResult> GetCuentasVencidas()
        {
            var cuentas = await _context.CuentasAhorro
                .Include(c => c.Cliente).ThenInclude(cl => cl.Usuario)
                .Include(c => c.Producto)
                .Where(c => c.estatus == EstatusAhorro.VENCIDA)
                .OrderByDescending(c => c.created_at)
                .Select(c => new
                {
                    c.id,
                    c.cliente_id,
                    nombreCliente = (c.Cliente.Usuario.nombre ?? "") + " " + (c.Cliente.Usuario.apellido ?? ""),
                    nombreProducto = c.Producto.nombre,
                    c.monto_inicial,
                    c.saldo_actual,
                    c.fecha_apertura,
                    c.fecha_vencimiento,
                    estatus = c.estatus.ToString(),
                })
                .ToListAsync();
            return Ok(cuentas);
        }

        // =====================================================
        // GET /api/Ahorro/cuentas/{id}
        // Detalle de cuenta + movimientos
        // =====================================================
        [Authorize]
        [HttpGet("cuentas/{id:int}")]
        public async Task<IActionResult> GetCuenta(int id)
        {
            var cuenta = await _context.CuentasAhorro
                .Include(c => c.Cliente).ThenInclude(cl => cl.Usuario)
                .Include(c => c.Producto)
                .Include(c => c.Movimientos)
                .Where(c => c.id == id)
                .Select(c => new
                {
                    c.id,
                    c.cliente_id,
                    nombreCliente = (c.Cliente.Usuario.nombre ?? "") + " " + (c.Cliente.Usuario.apellido ?? ""),
                    c.producto_ahorro_id,
                    nombreProducto = c.Producto.nombre,
                    c.Producto.tasa_anual,
                    c.Producto.plazo_dias,
                    c.monto_inicial,
                    c.saldo_actual,
                    c.fecha_apertura,
                    c.fecha_vencimiento,
                    estatus = c.estatus.ToString(),
                    c.ejecutivo_id,
                    c.created_at,
                    movimientos = c.Movimientos
                        .OrderByDescending(m => m.fecha)
                        .Select(m => new
                        {
                            m.id,
                            m.tipo,
                            m.monto,
                            m.descripcion,
                            m.fecha,
                            m.created_at,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            if (cuenta == null) return NotFound("Cuenta no encontrada");
            return Ok(cuenta);
        }

        // =====================================================
        // POST /api/Ahorro/apertura
        // Abre una cuenta nueva y registra depósito inicial
        // =====================================================
        [Authorize]
        [HttpPost("apertura")]
        public async Task<IActionResult> AbrirCuenta([FromBody] AperturaAhorroDto dto)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(c => c.cliente_id == dto.cliente_id);
            if (cliente == null) return NotFound("Cliente no encontrado");

            var producto = await _context.ProductosAhorro
                .FirstOrDefaultAsync(p => p.id == dto.producto_ahorro_id && p.activo);
            if (producto == null) return NotFound("Producto de ahorro no encontrado");

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var cuenta = new CuentaAhorro
            {
                cliente_id        = dto.cliente_id,
                producto_ahorro_id = dto.producto_ahorro_id,
                monto_inicial     = dto.monto_inicial,
                saldo_actual      = dto.monto_inicial,
                fecha_apertura    = hoy,
                fecha_vencimiento = hoy.AddDays(producto.plazo_dias),
                estatus           = EstatusAhorro.ACTIVA,
                ejecutivo_id      = dto.ejecutivo_id,
                created_at        = DateTime.UtcNow,
            };

            _context.CuentasAhorro.Add(cuenta);
            await _context.SaveChangesAsync();

            var deposito = new MovimientoAhorro
            {
                cuenta_ahorro_id = cuenta.id,
                tipo             = "DEPOSITO",
                monto            = dto.monto_inicial,
                descripcion      = $"Depósito inicial — apertura cuenta {producto.nombre}",
                fecha            = hoy,
                created_at       = DateTime.UtcNow,
            };
            _context.MovimientosAhorro.Add(deposito);
            await _context.SaveChangesAsync();

            await _activity.CreateActivity(
                ActivityType.CREDIT_APPROVED,
                dto.cliente_id,
                dto.monto_inicial,
                NotificationLevel.POSITIVE,
                $"Cuenta de ahorro aperturada por ${dto.monto_inicial:N2} — {producto.nombre}",
                dto.ejecutivo_id
            );

            return CreatedAtAction(nameof(GetCuenta), new { id = cuenta.id }, new { cuenta.id });
        }

        // =====================================================
        // POST /api/Ahorro/rendimiento/{id}
        // Aplica rendimiento mensual: saldo × (tasa / 100 / 12)
        // =====================================================
        [Authorize]
        [HttpPost("rendimiento/{id:int}")]
        public async Task<IActionResult> AplicarRendimiento(int id)
        {
            var cuenta = await _context.CuentasAhorro
                .Include(c => c.Producto)
                .FirstOrDefaultAsync(c => c.id == id);

            if (cuenta == null) return NotFound("Cuenta no encontrada");
            if (cuenta.estatus != EstatusAhorro.ACTIVA)
                return BadRequest("Solo se puede aplicar rendimiento a cuentas ACTIVAS");

            if (cuenta.Producto.tasa_anual == 0)
                return BadRequest("Este producto no genera rendimientos (tasa 0%)");

            var rendimiento = Math.Round(cuenta.saldo_actual * (cuenta.Producto.tasa_anual / 100m / 12m), 2);
            cuenta.saldo_actual += rendimiento;

            var movimiento = new MovimientoAhorro
            {
                cuenta_ahorro_id = cuenta.id,
                tipo             = "RENDIMIENTO",
                monto            = rendimiento,
                descripcion      = $"Rendimiento mensual ({cuenta.Producto.tasa_anual}% anual / 12)",
                fecha            = DateOnly.FromDateTime(DateTime.Today),
                created_at       = DateTime.UtcNow,
            };
            _context.MovimientosAhorro.Add(movimiento);
            await _context.SaveChangesAsync();

            return Ok(new { rendimiento, saldo_actual = cuenta.saldo_actual });
        }

        // =====================================================
        // PUT /api/Ahorro/cancelar/{id}
        // Cancela una cuenta de ahorro
        // =====================================================
        [Authorize]
        [HttpPut("cancelar/{id:int}")]
        public async Task<IActionResult> CancelarCuenta(int id)
        {
            var cuenta = await _context.CuentasAhorro.FindAsync(id);
            if (cuenta == null) return NotFound("Cuenta no encontrada");
            if (cuenta.estatus == EstatusAhorro.CANCELADA)
                return BadRequest("La cuenta ya está cancelada");

            cuenta.estatus = EstatusAhorro.CANCELADA;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Cuenta cancelada exitosamente" });
        }
    }

    // DTO para apertura
    public class AperturaAhorroDto
    {
        public int     cliente_id         { get; set; }
        public int     producto_ahorro_id { get; set; }
        public decimal monto_inicial      { get; set; }
        public int?    ejecutivo_id       { get; set; }
    }
}
