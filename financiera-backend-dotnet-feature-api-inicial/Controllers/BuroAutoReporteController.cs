using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Models;

namespace ApiEjemplo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BuroAutoReporteController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BuroAutoReporteController(AppDbContext db)
        {
            _db = db;
        }

        // POST /api/BuroAutoReporte — reporte manual de un cliente al buró antes de que
        // el cron de mora >= 90 días lo haga automáticamente (crea o actualiza la fila).
        [HttpPost]
        public async Task<IActionResult> Reportar([FromBody] BuroAutoReporte dto)
        {
            try
            {
                if (dto.cliente_id <= 0 || dto.prestamo_id <= 0)
                    return BadRequest("cliente_id y prestamo_id son requeridos");

                var existing = await _db.BuroAutoReportes
                    .FirstOrDefaultAsync(b => b.cliente_id == dto.cliente_id);
                if (existing != null)
                {
                    existing.prestamo_id     = dto.prestamo_id;
                    existing.fecha_reporte   = DateTime.UtcNow;
                    existing.dias_mora       = dto.dias_mora;
                    existing.saldo_pendiente = dto.saldo_pendiente;
                    existing.motivo          = dto.motivo ?? "REPORTADO MANUALMENTE por administrador";
                }
                else
                {
                    _db.BuroAutoReportes.Add(new BuroAutoReporte
                    {
                        cliente_id      = dto.cliente_id,
                        prestamo_id     = dto.prestamo_id,
                        fecha_reporte   = DateTime.UtcNow,
                        dias_mora       = dto.dias_mora,
                        saldo_pendiente = dto.saldo_pendiente,
                        motivo          = dto.motivo ?? "REPORTADO MANUALMENTE por administrador",
                    });
                }

                // Un reporte manual anula cualquier exclusión previa del mismo cliente
                var exclusion = await _db.BuroExclusiones.FindAsync(dto.cliente_id);
                if (exclusion != null) _db.BuroExclusiones.Remove(exclusion);

                await _db.SaveChangesAsync();
                return Ok(new { message = "Cliente reportado al buró correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // GET /api/BuroAutoReporte — lista todos los clientes auto-reportados por mora >= 90 días
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var lista = await _db.BuroAutoReportes
                    .OrderByDescending(b => b.dias_mora)
                    .Select(b => new {
                        b.cliente_id,
                        b.prestamo_id,
                        b.dias_mora,
                        b.saldo_pendiente,
                        b.motivo,
                        fecha_reporte = b.fecha_reporte.ToString("yyyy-MM-dd HH:mm"),
                    })
                    .ToListAsync();

                return Ok(lista);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // GET /api/BuroAutoReporte/exportar — todos los registros con datos completos para el Excel BC
        [HttpGet("exportar")]
        public async Task<IActionResult> Exportar()
        {
            try
            {
                var lista = await _db.BuroAutoReportes
                    .Join(_db.Prestamos,
                          b => b.prestamo_id,
                          p => p.prestamo_id,
                          (b, p) => new { b, p })
                    .Join(_db.Clientes,
                          bp => bp.b.cliente_id,
                          c => c.cliente_id,
                          (bp, c) => new { bp.b, bp.p, c })
                    .Join(_db.Usuarios,
                          bpc => bpc.c.usuario_id,
                          u => u.usuario_id,
                          (bpc, u) => new {
                              // buro_auto_reporte
                              prestamo_id       = bpc.b.prestamo_id,
                              cliente_id        = bpc.b.cliente_id,
                              dias_mora         = bpc.b.dias_mora,
                              saldo_pendiente   = bpc.b.saldo_pendiente,
                              fecha_reporte     = bpc.b.fecha_reporte.ToString("yyyy-MM-dd"),
                              motivo            = bpc.b.motivo,
                              // prestamo
                              forma_pago        = bpc.p.forma_pago.ToString(),
                              plazo_meses       = bpc.p.plazo_meses,
                              pago_mes          = bpc.p.pago_mes,
                              fecha_inicio      = bpc.p.fecha_inicio.ToString("yyyy-MM-dd"),
                              monto             = bpc.p.monto,
                              monto_total       = bpc.p.monto_total,
                              saldo_actual      = bpc.p.saldo_actual,
                              administrado_en   = bpc.p.administrado_en,
                              estatus           = bpc.p.estatus.ToString(),
                              fecha_fin          = (DateTime?)bpc.p.fecha_fin,
                              fecha_proximo_pago = (DateTime?)bpc.p.fecha_proximo_pago,
                              // cliente
                              apellido_paterno  = bpc.c.apellido_paterno,
                              apellido_materno  = bpc.c.apellido_materno,
                              fecha_nacimiento  = bpc.c.fecha_nacimiento.HasValue ? bpc.c.fecha_nacimiento.Value.ToString("yyyy-MM-dd") : null,
                              curp              = bpc.c.curp,
                              rfc               = bpc.c.rfc,
                              sexo              = bpc.c.sexo,
                              estado_civil      = bpc.c.estado_civil,
                              empresa_nombre    = bpc.c.empresa_nombre,
                              calle             = string.IsNullOrEmpty(bpc.c.calle) ? bpc.c.direccion : bpc.c.calle,
                              colonia           = bpc.c.colonia,
                              municipio         = bpc.c.municipio,
                              ciudad            = bpc.c.ciudad,
                              cp                = bpc.c.cp,
                              telefono          = bpc.c.telefono_particular,
                              ruta_vinculacion  = bpc.c.ruta_vinculacion,
                              estado_domicilio  = bpc.c.estado_domicilio,
                              num_ext           = bpc.c.num_ext,
                              // usuario
                              nombre_cliente    = u.nombre,
                              apellido_usuario  = u.apellido,
                              numero_pagos_vencidos = _db.PeriodosAmortizacion
                                  .Count(pa => pa.prestamo_id == bpc.b.prestamo_id && pa.estado_pago == 1),
                              fecha_primer_incumplimiento = _db.PeriodosAmortizacion
                                  .Where(pa => pa.prestamo_id == bpc.b.prestamo_id && pa.estado_pago == 1 && pa.fecha_vencimiento.Year > 2000)
                                  .OrderBy(pa => pa.fecha_vencimiento)
                                  .Select(pa => (DateTime?)pa.fecha_vencimiento)
                                  .FirstOrDefault(),
                              ultimo_pago = _db.Pagos
                                  .Where(pg => pg.prestamo_id == bpc.b.prestamo_id)
                                  .OrderByDescending(pg => pg.fecha_pago)
                                  .Select(pg => (DateTime?)pg.fecha_pago)
                                  .FirstOrDefault()
                          })
                    .OrderBy(x => x.prestamo_id)
                    .ToListAsync();

                return Ok(lista.Select(x => new {
                    x.prestamo_id,
                    x.cliente_id,
                    x.dias_mora,
                    x.saldo_pendiente,
                    x.fecha_reporte,
                    x.motivo,
                    x.forma_pago,
                    x.plazo_meses,
                    x.pago_mes,
                    x.fecha_inicio,
                    x.monto,
                    x.monto_total,
                    x.saldo_actual,
                    x.administrado_en,
                    x.estatus,
                    x.apellido_paterno,
                    x.apellido_materno,
                    x.fecha_nacimiento,
                    x.curp,
                    x.rfc,
                    x.sexo,
                    x.estado_civil,
                    x.empresa_nombre,
                    x.calle,
                    x.colonia,
                    x.municipio,
                    x.ciudad,
                    x.cp,
                    x.telefono,
                    x.ruta_vinculacion,
                    x.estado_domicilio,
                    x.num_ext,
                    x.nombre_cliente,
                    x.apellido_usuario,
                    x.numero_pagos_vencidos,
                    fecha_cierre = (x.estatus == "LIQUIDADO" || x.estatus == "CANCELADO")
                        && x.fecha_fin.HasValue && x.fecha_fin.Value.Year > 2000
                        ? x.fecha_fin.Value.ToString("yyyy-MM-dd")
                        : null,
                    ultimo_pago = x.ultimo_pago.HasValue ? x.ultimo_pago.Value.ToString("yyyy-MM-dd") : null,
                    fecha_primer_incumplimiento = (x.fecha_primer_incumplimiento.HasValue && x.fecha_primer_incumplimiento.Value.Year > 2000)
                        ? x.fecha_primer_incumplimiento.Value.ToString("yyyy-MM-dd")
                        : (x.fecha_proximo_pago.HasValue && x.fecha_proximo_pago.Value.Year > 2000
                            ? x.fecha_proximo_pago.Value.ToString("yyyy-MM-dd")
                            : null)
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // DELETE /api/BuroAutoReporte/{clienteId} — quitar un cliente del auto-reporte (manual override)
        // MONEYPINE-FIX: estaba abierto a cualquier rol autenticado. Quitar el
        // auto-reporte borra el registro de mora en buro, asi que solo ADMIN.
        // Los demas roles lo piden via SolicitudAprobacion.
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{clienteId:int}")]
        public async Task<IActionResult> Quitar(int clienteId)
        {
            try
            {
                // FindAsync falla con clave compuesta (cliente_id + prestamo_id); usar FirstOrDefaultAsync
                var existing = await _db.BuroAutoReportes
                    .FirstOrDefaultAsync(b => b.cliente_id == clienteId);
                if (existing == null) return NotFound();
                _db.BuroAutoReportes.Remove(existing);

                // Agregar a exclusión para que el cron job no lo vuelva a reportar
                var exclusion = await _db.BuroExclusiones.FindAsync(clienteId);
                if (exclusion == null)
                {
                    _db.BuroExclusiones.Add(new Models.BuroExclusion
                    {
                        cliente_id   = clienteId,
                        excluido_por = null,
                        fecha        = DateTime.UtcNow,
                        motivo       = "Quitado manualmente de lista negra por administrador",
                    });
                }

                await _db.SaveChangesAsync();
                return Ok(new { message = "Cliente removido del auto-reporte" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }
}
