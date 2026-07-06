using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;

namespace ApiEjemplo.Services
{
    public class ListaNegraService
    {
        private readonly AppDbContext _db;

        public ListaNegraService(AppDbContext db)
        {
            _db = db;
        }

        // Criterios oficiales de elegibilidad
        private const int UMBRAL_DIAS_MORA  = 130;
        private const decimal UMBRAL_MONTO_MORA = 1500m;

        // Calcula dias_mora_bd y monto_mora_bd para un préstamo usando periodo_amortizacion.
        // Solo considera períodos con estado_pago IN (1=pendiente, 5=mora congelada).
        // No resta ahorro_por_pago: los períodos con estado_pago=3 están pagados y quedan excluidos.
        public async Task<(int dias, decimal monto)> ObtenerMoraBdAsync(int prestamoId)
        {
            var periodos = await _db.PeriodosAmortizacion
                .Where(pa => pa.prestamo_id == prestamoId && (pa.estado_pago == 1 || pa.estado_pago == 5))
                .ToListAsync();

            if (!periodos.Any()) return (0, 0m);

            int maxDias      = periodos.Max(pa => pa.dias_moratorio);
            decimal sumaMore = periodos.Sum(pa => pa.interes_moratorio);
            return (maxDias, sumaMore);
        }

        // Determina si un préstamo es candidato a lista negra según las reglas de negocio.
        public async Task<bool> EsCandidatoAsync(Prestamo prestamo)
        {
            if (prestamo.estatus == EstatusPrestamo.LIQUIDADO ||
                prestamo.estatus == EstatusPrestamo.CANCELADO)
                return false;

            var (dias, monto) = await ObtenerMoraBdAsync(prestamo.prestamo_id);
            return dias > UMBRAL_DIAS_MORA && monto > UMBRAL_MONTO_MORA;
        }

        // Devuelve todos los registros de lista_negra con estado ACTIVO.
        public async Task<List<ListaNegra>> ObtenerActivosAsync()
        {
            return await _db.ListasNegras
                .Where(ln => ln.estado == "ACTIVO")
                .OrderByDescending(ln => ln.fecha_alta)
                .ToListAsync();
        }

        // Devuelve los préstamos candidatos que aún no tienen una entrada ACTIVA en lista_negra.
        public async Task<List<CandidatoListaNegraDto>> ObtenerCandidatosAsync()
        {
            var prestamos = await _db.Prestamos
                .Where(p => p.estatus != EstatusPrestamo.LIQUIDADO &&
                            p.estatus != EstatusPrestamo.CANCELADO)
                .ToListAsync();

            var activosSet = (await _db.ListasNegras
                .Where(ln => ln.estado == "ACTIVO")
                .Select(ln => new { ln.cliente_id, ln.prestamo_id })
                .ToListAsync())
                .ToHashSet();

            var candidatos = new List<CandidatoListaNegraDto>();

            foreach (var p in prestamos)
            {
                var (dias, monto) = await ObtenerMoraBdAsync(p.prestamo_id);
                if (dias <= UMBRAL_DIAS_MORA || monto <= UMBRAL_MONTO_MORA) continue;

                bool yaEsta = activosSet.Any(a => a.cliente_id == p.cliente_id && a.prestamo_id == p.prestamo_id);
                candidatos.Add(new CandidatoListaNegraDto
                {
                    cliente_id  = p.cliente_id,
                    prestamo_id = p.prestamo_id,
                    estatus     = p.estatus.ToString(),
                    dias_mora   = dias,
                    monto_mora  = monto,
                    ya_en_lista = yaEsta,
                });
            }

            return candidatos.OrderByDescending(c => c.dias_mora).ToList();
        }

        // Agrega un cliente a lista_negra. Si ya existe entrada ACTIVA para (cliente_id, prestamo_id)
        // devuelve false sin duplicar.
        public async Task<(bool ok, string mensaje)> AgregarAsync(
            int clienteId, int? prestamoId, string motivo, string origen, int? creadoPor, string? observaciones)
        {
            bool duplicado = await _db.ListasNegras.AnyAsync(ln =>
                ln.cliente_id  == clienteId &&
                ln.prestamo_id == prestamoId &&
                ln.estado      == "ACTIVO");

            if (duplicado)
                return (false, "Ya existe una entrada activa para este cliente/préstamo.");

            var ahora = DateTime.UtcNow;
            _db.ListasNegras.Add(new ListaNegra
            {
                cliente_id       = clienteId,
                prestamo_id      = prestamoId,
                motivo           = motivo,
                dias_mora        = 0,
                monto_mora       = 0m,
                estado           = "ACTIVO",
                origen           = origen,
                fecha_alta       = ahora,
                creado_por       = creadoPor,
                observaciones    = observaciones,
                fecha_creacion   = ahora,
            });

            await _db.SaveChangesAsync();
            return (true, "Cliente agregado a lista negra.");
        }

        // Agrega con datos de mora ya calculados (usado por sincronizar).
        public Task AgregarConMoraAsync(
            int clienteId, int prestamoId, int dias, decimal monto, int? creadoPor)
        {
            var ahora = DateTime.UtcNow;
            _db.ListasNegras.Add(new ListaNegra
            {
                cliente_id     = clienteId,
                prestamo_id    = prestamoId,
                motivo         = $"AUTO: {dias} días de mora, ${monto:F2} acumulado",
                dias_mora      = dias,
                monto_mora     = monto,
                estado         = "ACTIVO",
                origen         = "AUTOMATICO",
                fecha_alta     = ahora,
                creado_por     = creadoPor,
                fecha_creacion = ahora,
            });
            return Task.CompletedTask;
        }

        // Marca una entrada como REMOVIDA. No borra físico.
        public async Task<bool> RemoverAsync(int listaNegraId, int? actualizadoPor)
        {
            var entry = await _db.ListasNegras.FindAsync(listaNegraId);
            if (entry == null) return false;

            var ahora = DateTime.UtcNow;
            entry.estado              = "REMOVIDO";
            entry.fecha_baja          = ahora;
            entry.actualizado_por     = actualizadoPor;
            entry.fecha_actualizacion = ahora;

            await _db.SaveChangesAsync();
            return true;
        }

        // Sincroniza la lista negra:
        // 1. Agrega candidatos automáticos que aún no están activos.
        // 2. Remueve activos cuyo préstamo está LIQUIDADO/CANCELADO.
        // 3. Remueve activos que ya no cumplen los criterios.
        public async Task<SincronizarResultadoDto> SincronizarAsync(int? usuarioId)
        {
            int agregados = 0, removidos = 0;

            var activos = await _db.ListasNegras
                .Where(ln => ln.estado == "ACTIVO" && ln.prestamo_id != null)
                .ToListAsync();

            var prestamosMap = await _db.Prestamos
                .Where(p => activos.Select(a => a.prestamo_id!.Value).Contains(p.prestamo_id) ||
                            (p.estatus != EstatusPrestamo.LIQUIDADO && p.estatus != EstatusPrestamo.CANCELADO))
                .ToDictionaryAsync(p => p.prestamo_id);

            // Remover activos que ya no cumplen
            var ahora = DateTime.UtcNow;
            foreach (var entry in activos)
            {
                bool remover = false;
                if (!prestamosMap.TryGetValue(entry.prestamo_id!.Value, out var p))
                {
                    remover = true;
                }
                else if (p.estatus == EstatusPrestamo.LIQUIDADO || p.estatus == EstatusPrestamo.CANCELADO)
                {
                    entry.motivo = "Crédito liquidado o cancelado";
                    remover = true;
                }
                else
                {
                    var (dias, monto) = await ObtenerMoraBdAsync(p.prestamo_id);
                    if (dias <= UMBRAL_DIAS_MORA || monto <= UMBRAL_MONTO_MORA)
                    {
                        entry.motivo = "Ya no cumple criterios de mora";
                        remover = true;
                    }
                    else
                    {
                        // Actualizar valores de mora
                        entry.dias_mora           = dias;
                        entry.monto_mora          = monto;
                        entry.fecha_actualizacion = ahora;
                        entry.actualizado_por     = usuarioId;
                    }
                }

                if (remover)
                {
                    entry.estado              = "REMOVIDO";
                    entry.fecha_baja          = ahora;
                    entry.actualizado_por     = usuarioId;
                    entry.fecha_actualizacion = ahora;
                    removidos++;
                }
            }

            await _db.SaveChangesAsync();

            // Agregar nuevos candidatos
            var activosActualizados = (await _db.ListasNegras
                .Where(ln => ln.estado == "ACTIVO")
                .Select(ln => new { ln.cliente_id, ln.prestamo_id })
                .ToListAsync())
                .ToHashSet();

            var candidatosPrestamos = await _db.Prestamos
                .Where(p => p.estatus != EstatusPrestamo.LIQUIDADO &&
                            p.estatus != EstatusPrestamo.CANCELADO)
                .ToListAsync();

            foreach (var p in candidatosPrestamos)
            {
                if (activosActualizados.Any(a => a.cliente_id == p.cliente_id && a.prestamo_id == p.prestamo_id))
                    continue;

                var (dias, monto) = await ObtenerMoraBdAsync(p.prestamo_id);
                if (dias <= UMBRAL_DIAS_MORA || monto <= UMBRAL_MONTO_MORA) continue;

                await AgregarConMoraAsync(p.cliente_id, p.prestamo_id, dias, monto, usuarioId);
                agregados++;
            }

            await _db.SaveChangesAsync();

            return new SincronizarResultadoDto { agregados = agregados, removidos = removidos };
        }
    }

    public class CandidatoListaNegraDto
    {
        public int     cliente_id  { get; set; }
        public int     prestamo_id { get; set; }
        public string  estatus     { get; set; } = string.Empty;
        public int     dias_mora   { get; set; }
        public decimal monto_mora  { get; set; }
        public bool    ya_en_lista { get; set; }
    }

    public class SincronizarResultadoDto
    {
        public int agregados { get; set; }
        public int removidos { get; set; }
    }
}
