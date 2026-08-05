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
    [Authorize]
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
            existing.tipo        = dto.tipo;
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
            var c = await _context.CuentasAhorro
                .Include(x => x.Cliente).ThenInclude(cl => cl.Usuario)
                .Include(x => x.Producto)
                .Include(x => x.Movimientos)
                .FirstOrDefaultAsync(x => x.id == id);

            if (c == null) return NotFound("Cuenta no encontrada");

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            return Ok(new
            {
                c.id,
                c.cliente_id,
                nombreCliente = (c.Cliente.Usuario?.nombre ?? "") + " " + (c.Cliente.Usuario?.apellido ?? ""),
                c.producto_ahorro_id,
                nombreProducto = c.Producto.nombre,
                c.Producto.tasa_anual,
                c.Producto.plazo_dias,
                tipo_producto = c.Producto.tipo.ToString(),
                c.monto_inicial,
                c.saldo_actual,
                c.fecha_apertura,
                c.fecha_vencimiento,
                estatus = c.estatus.ToString(),
                c.ejecutivo_id,
                c.created_at,

                // ── Rendimiento ──
                c.rendimiento_acumulado,
                c.fecha_ultimo_rendimiento,
                // Ganado pero aun no capitalizado (lo que el cliente "lleva" hoy)
                rendimiento_pendiente = RendimientoAhorroService.RendimientoPendiente(c, hoy),
                // Cuanto ganara si lo deja hasta el vencimiento
                proyeccion_vencimiento = RendimientoAhorroService.ProyeccionAlVencimiento(c, hoy),
                saldo_estimado_hoy = c.saldo_actual + RendimientoAhorroService.RendimientoPendiente(c, hoy),

                // ── Disponibilidad ──
                permite_retiro   = RendimientoAhorroService.PermiteRetiroLibre(c, hoy),
                dias_para_retiro = Math.Max(0, c.fecha_vencimiento.DayNumber - hoy.DayNumber),

                movimientos = c.Movimientos
                    .OrderByDescending(m => m.fecha).ThenByDescending(m => m.id)
                    .Select(m => new { m.id, m.tipo, m.monto, m.descripcion, m.fecha, m.created_at })
                    .ToList(),
            });
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

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var (rendimiento, dias) = await CapitalizarAsync(cuenta, hoy);

            if (rendimiento <= 0)
                return Ok(new
                {
                    message      = "No hay dias pendientes por capitalizar.",
                    rendimiento  = 0m,
                    dias         = 0,
                    saldo_actual = cuenta.saldo_actual,
                });

            await _context.SaveChangesAsync();
            return Ok(new { rendimiento, dias, saldo_actual = cuenta.saldo_actual });
        }

        // =====================================================
        // POST /api/Ahorro/rendimiento/aplicar-todas
        // Capitaliza TODAS las cuentas activas. Pensado para correrse a diario.
        // Idempotente: si ya se corrio hoy, no vuelve a pagar. Solo ADMIN.
        // =====================================================
        [Authorize(Roles = "ADMIN")]
        [HttpPost("rendimiento/aplicar-todas")]
        public async Task<IActionResult> AplicarRendimientoTodas()
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var cuentas = await _context.CuentasAhorro
                .Include(c => c.Producto)
                .Where(c => c.estatus == EstatusAhorro.ACTIVA)
                .ToListAsync();

            decimal total = 0m;
            int afectadas = 0;
            foreach (var cuenta in cuentas)
            {
                var (rendimiento, _) = await CapitalizarAsync(cuenta, hoy);
                if (rendimiento > 0) { total += rendimiento; afectadas++; }
            }
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message            = $"Rendimiento aplicado a {afectadas} cuenta(s).",
                cuentas_revisadas  = cuentas.Count,
                cuentas_afectadas  = afectadas,
                total_abonado      = Math.Round(total, 2),
            });
        }

        // =====================================================
        // POST /api/Ahorro/deposito/{id}
        // Abona dinero a una cuenta de ahorro.
        // Antes capitaliza lo pendiente, para que el deposito no gane
        // rendimiento de dias en los que aun no existia.
        // =====================================================
        [Authorize]
        [HttpPost("deposito/{id:int}")]
        public async Task<IActionResult> Depositar(int id, [FromBody] MovimientoAhorroDto dto)
        {
            if (dto.monto <= 0) return BadRequest("El monto debe ser mayor a cero");

            var cuenta = await _context.CuentasAhorro
                .Include(c => c.Producto)
                .FirstOrDefaultAsync(c => c.id == id);

            if (cuenta == null) return NotFound("Cuenta no encontrada");
            if (cuenta.estatus != EstatusAhorro.ACTIVA)
                return BadRequest("Solo se puede depositar en cuentas ACTIVAS");

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            await CapitalizarAsync(cuenta, hoy);

            cuenta.saldo_actual += dto.monto;
            _context.MovimientosAhorro.Add(new MovimientoAhorro
            {
                cuenta_ahorro_id = cuenta.id,
                tipo             = "DEPOSITO",
                monto            = dto.monto,
                descripcion      = string.IsNullOrWhiteSpace(dto.descripcion) ? "Depósito" : dto.descripcion!.Trim(),
                fecha            = hoy,
                created_at       = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Depósito aplicado.", saldo_actual = cuenta.saldo_actual });
        }

        // =====================================================
        // POST /api/Ahorro/retiro/{id}
        // Retira dinero. En productos PLAZO_FIJO no vencidos el retiro esta
        // bloqueado; solo un ADMIN puede autorizarlo y queda registrado.
        // =====================================================
        [Authorize]
        [HttpPost("retiro/{id:int}")]
        public async Task<IActionResult> Retirar(int id, [FromBody] MovimientoAhorroDto dto)
        {
            if (dto.monto <= 0) return BadRequest("El monto debe ser mayor a cero");

            var cuenta = await _context.CuentasAhorro
                .Include(c => c.Producto)
                .FirstOrDefaultAsync(c => c.id == id);

            if (cuenta == null) return NotFound("Cuenta no encontrada");
            if (cuenta.estatus != EstatusAhorro.ACTIVA)
                return BadRequest("Solo se puede retirar de cuentas ACTIVAS");

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            // Candado de plazo fijo
            bool anticipado = !RendimientoAhorroService.PermiteRetiroLibre(cuenta, hoy);
            if (anticipado && !User.IsInRole("ADMIN"))
            {
                var faltan = cuenta.fecha_vencimiento.DayNumber - hoy.DayNumber;
                return StatusCode(403, new
                {
                    message = $"Cuenta a plazo fijo: el retiro se habilita el {cuenta.fecha_vencimiento:yyyy-MM-dd} " +
                              $"(faltan {faltan} día(s)). Un administrador puede autorizar el retiro anticipado.",
                    fecha_vencimiento = cuenta.fecha_vencimiento,
                    dias_faltantes    = faltan,
                    requiere_admin    = true,
                });
            }

            // Capitalizar antes de retirar: el cliente se lleva lo ganado hasta hoy
            await CapitalizarAsync(cuenta, hoy);

            if (dto.monto > cuenta.saldo_actual)
                return BadRequest(new
                {
                    message = $"Saldo insuficiente. Disponible: {cuenta.saldo_actual:C2}",
                    saldo_actual = cuenta.saldo_actual,
                });

            cuenta.saldo_actual -= dto.monto;

            var etiqueta = anticipado ? "RETIRO ANTICIPADO (autorizado por ADMIN)" : "Retiro";
            _context.MovimientosAhorro.Add(new MovimientoAhorro
            {
                cuenta_ahorro_id = cuenta.id,
                tipo             = "RETIRO",
                monto            = dto.monto,
                descripcion      = string.IsNullOrWhiteSpace(dto.descripcion) ? etiqueta : $"{etiqueta} — {dto.descripcion!.Trim()}",
                fecha            = hoy,
                created_at       = DateTime.UtcNow,
            });

            // Un retiro anticipado rompe la regla del producto: queda auditado
            if (anticipado)
            {
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(idClaim, out var usuarioId);
                await _activity.CreateActivity(
                    ActivityType.CUSTOM,
                    cuenta.cliente_id,
                    dto.monto,
                    NotificationLevel.HIGH,
                    $"Retiro anticipado autorizado en cuenta de ahorro #{cuenta.id} " +
                    $"({cuenta.Producto.nombre}): {dto.monto:C2}. Vencía el {cuenta.fecha_vencimiento:yyyy-MM-dd}.",
                    usuarioId == 0 ? null : usuarioId
                );
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message      = anticipado ? "Retiro anticipado aplicado (autorizado)." : "Retiro aplicado.",
                saldo_actual = cuenta.saldo_actual,
                anticipado,
            });
        }

        // =====================================================
        // Capitaliza los dias pendientes de una cuenta.
        // NO llama a SaveChanges: quien la usa decide cuando persistir.
        // Devuelve (rendimiento abonado, dias capitalizados).
        // =====================================================
        private Task<(decimal rendimiento, int dias)> CapitalizarAsync(CuentaAhorro cuenta, DateOnly hoy)
        {
            var dias = RendimientoAhorroService.DiasPendientes(cuenta, hoy);
            var rendimiento = RendimientoAhorroService.CalcularRendimiento(
                cuenta.saldo_actual, cuenta.Producto?.tasa_anual ?? 0m, dias);

            if (rendimiento <= 0)
            {
                // Aun sin ganancia se avanza la marca para no reprocesar los mismos dias
                if (dias > 0) cuenta.fecha_ultimo_rendimiento = hoy;
                return Task.FromResult((0m, 0));
            }

            cuenta.saldo_actual           += rendimiento;
            cuenta.rendimiento_acumulado  += rendimiento;
            cuenta.fecha_ultimo_rendimiento = (cuenta.fecha_ultimo_rendimiento ?? cuenta.fecha_apertura).AddDays(dias);

            _context.MovimientosAhorro.Add(new MovimientoAhorro
            {
                cuenta_ahorro_id = cuenta.id,
                tipo             = "RENDIMIENTO",
                monto            = rendimiento,
                descripcion      = $"Rendimiento de {dias} día(s) — {cuenta.Producto?.tasa_anual}% anual (compuesto diario)",
                fecha            = hoy,
                created_at       = DateTime.UtcNow,
            });

            return Task.FromResult((rendimiento, dias));
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

    // Body de deposito y retiro
    public class MovimientoAhorroDto
    {
        public decimal monto       { get; set; }
        public string? descripcion { get; set; }
    }
}
