using ApiEjemplo.Data;
using ApiEjemplo.Enums;
using ApiEjemplo.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiEjemplo.Services
{
    /// <summary>
    /// Resultado de resolver una solicitud. Se devuelve un objeto en vez de
    /// lanzar excepciones porque "el pago ya no existe" no es un error del
    /// sistema: es un desenlace legitimo que el admin necesita entender.
    /// </summary>
    public record ResultadoResolucion(bool Ok, string Mensaje, EstadoSolicitud EstadoFinal);

    /// <summary>
    /// Crea y resuelve las peticiones que los roles no-ADMIN hacen para
    /// ejecutar acciones sensibles.
    ///
    /// La regla central: al crear NO se toca nada. La accion se aplica cuando
    /// el ADMIN aprueba, y solo despues de revalidar que el estado siga siendo
    /// el que el solicitante describio. Entre que se pide y se autoriza pueden
    /// pasar horas: el pago pudo eliminarse por otra via, el credito liquidarse
    /// o la mora recalcularse. Ejecutar a ciegas descuadraria la cartera.
    /// </summary>
    public class SolicitudAprobacionService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ActivityService _activityService;
        private readonly MotorRecalculoPrestamoService _motorRecalculo;

        public SolicitudAprobacionService(
            AppDbContext context,
            NotificationService notificationService,
            ActivityService activityService,
            MotorRecalculoPrestamoService motorRecalculo)
        {
            _context = context;
            _notificationService = notificationService;
            _activityService = activityService;
            _motorRecalculo = motorRecalculo;
        }

        // =================================================================
        // CREAR
        // =================================================================
        public async Task<SolicitudAprobacion> CrearAsync(
            TipoSolicitud tipo,
            int solicitanteId,
            int entidadId,
            string justificacion,
            int? clienteId = null,
            decimal monto = 0,
            string? descripcion = null,
            string? payload = null)
        {
            var solicitud = new SolicitudAprobacion
            {
                tipo           = tipo,
                estado         = EstadoSolicitud.PENDIENTE,
                solicitante_id = solicitanteId,
                entidad_id     = entidadId,
                cliente_id     = clienteId,
                monto          = monto,
                justificacion  = justificacion.Trim(),
                descripcion    = descripcion,
                payload        = payload,
            };

            _context.SolicitudesAprobacion.Add(solicitud);
            await _context.SaveChangesAsync();

            await NotificarAdminsAsync(solicitud);

            return solicitud;
        }

        /// <summary>
        /// Avisa a TODOS los administradores activos. No se manda al usuario 1
        /// fijo porque puede no existir o no ser admin; si manana hay tres
        /// admins, los tres deben ver la peticion en su campana.
        /// </summary>
        private async Task NotificarAdminsAsync(SolicitudAprobacion solicitud)
        {
            var solicitante = await _context.Usuarios.FindAsync(solicitud.solicitante_id);
            var quien = solicitante == null
                ? $"Usuario #{solicitud.solicitante_id}"
                : $"{solicitante.nombre} {solicitante.apellido}".Trim();

            var mensaje = $"[Solicitud #{solicitud.id}] {quien} solicita: " +
                          $"{solicitud.descripcion ?? solicitud.tipo.ToString()}";

            var adminIds = await _context.Usuarios
                .Where(u => u.rol == RolUsuario.ADMIN && u.estado == EstadoUsuario.ACTIVO)
                .Select(u => u.usuario_id)
                .ToListAsync();

            foreach (var adminId in adminIds)
                await _notificationService.CreateNotification(adminId, mensaje, NotificationLevel.HIGH);
        }

        // =================================================================
        // APROBAR
        // =================================================================
        public async Task<ResultadoResolucion> AprobarAsync(int solicitudId, int adminId, string? respuesta)
        {
            var solicitud = await _context.SolicitudesAprobacion.FindAsync(solicitudId);
            if (solicitud == null)
                return new ResultadoResolucion(false, "Solicitud no encontrada.", EstadoSolicitud.PENDIENTE);

            if (solicitud.estado != EstadoSolicitud.PENDIENTE)
                return new ResultadoResolucion(false,
                    $"La solicitud ya fue resuelta ({solicitud.estado}).", solicitud.estado);

            // Revalidar contra el estado actual antes de tocar nada.
            var (vigente, motivo) = await RevalidarAsync(solicitud);
            if (!vigente)
            {
                solicitud.estado      = EstadoSolicitud.OBSOLETA;
                solicitud.resuelta_por = adminId;
                solicitud.resuelta_at  = DateTime.UtcNow;
                solicitud.respuesta    = $"No se pudo aplicar: {motivo}";
                await _context.SaveChangesAsync();
                await NotificarSolicitanteAsync(solicitud, aprobada: false);

                return new ResultadoResolucion(false, motivo, EstadoSolicitud.OBSOLETA);
            }

            var ejecucion = await EjecutarAsync(solicitud, adminId);
            if (!ejecucion.Ok)
                return ejecucion;

            solicitud.estado       = EstadoSolicitud.APROBADA;
            solicitud.resuelta_por = adminId;
            solicitud.resuelta_at  = DateTime.UtcNow;
            solicitud.respuesta    = string.IsNullOrWhiteSpace(respuesta) ? null : respuesta.Trim();
            await _context.SaveChangesAsync();

            await NotificarSolicitanteAsync(solicitud, aprobada: true);

            return new ResultadoResolucion(true, ejecucion.Mensaje, EstadoSolicitud.APROBADA);
        }

        // =================================================================
        // RECHAZAR
        // =================================================================
        public async Task<ResultadoResolucion> RechazarAsync(int solicitudId, int adminId, string respuesta)
        {
            var solicitud = await _context.SolicitudesAprobacion.FindAsync(solicitudId);
            if (solicitud == null)
                return new ResultadoResolucion(false, "Solicitud no encontrada.", EstadoSolicitud.PENDIENTE);

            if (solicitud.estado != EstadoSolicitud.PENDIENTE)
                return new ResultadoResolucion(false,
                    $"La solicitud ya fue resuelta ({solicitud.estado}).", solicitud.estado);

            solicitud.estado       = EstadoSolicitud.RECHAZADA;
            solicitud.resuelta_por = adminId;
            solicitud.resuelta_at  = DateTime.UtcNow;
            solicitud.respuesta    = respuesta.Trim();
            await _context.SaveChangesAsync();

            await NotificarSolicitanteAsync(solicitud, aprobada: false);

            return new ResultadoResolucion(true, "Solicitud rechazada.", EstadoSolicitud.RECHAZADA);
        }

        private async Task NotificarSolicitanteAsync(SolicitudAprobacion solicitud, bool aprobada)
        {
            var veredicto = solicitud.estado switch
            {
                EstadoSolicitud.APROBADA  => "APROBADA",
                EstadoSolicitud.RECHAZADA => "RECHAZADA",
                EstadoSolicitud.OBSOLETA  => "NO APLICABLE",
                _                         => solicitud.estado.ToString(),
            };

            var mensaje = $"[Solicitud #{solicitud.id}] {veredicto}: " +
                          $"{solicitud.descripcion ?? solicitud.tipo.ToString()}" +
                          (string.IsNullOrWhiteSpace(solicitud.respuesta)
                              ? ""
                              : $" — {solicitud.respuesta}");

            await _notificationService.CreateNotification(
                solicitud.solicitante_id,
                mensaje,
                aprobada ? NotificationLevel.POSITIVE : NotificationLevel.HIGH);
        }

        // =================================================================
        // REVALIDACIÓN — ¿el mundo sigue como cuando se pidió?
        // =================================================================
        private async Task<(bool vigente, string motivo)> RevalidarAsync(SolicitudAprobacion s)
        {
            switch (s.tipo)
            {
                case TipoSolicitud.ELIMINAR_PAGO:
                {
                    var pago = await _context.Pagos.FindAsync(s.entidad_id);
                    if (pago == null)
                        return (false, $"El pago #{s.entidad_id} ya no existe.");

                    if (pago.monto_pagado != s.monto)
                        return (false,
                            $"El monto del pago cambió (era {s.monto:C2}, ahora {pago.monto_pagado:C2}). " +
                            "Vuelve a solicitarlo con los datos actuales.");

                    return (true, "");
                }

                case TipoSolicitud.CONDONAR_MORA:
                {
                    var prestamo = await _context.Prestamos.FindAsync(s.entidad_id);
                    if (prestamo == null)
                        return (false, $"El crédito #{s.entidad_id} ya no existe.");

                    var moraActual = await _context.PeriodosAmortizacion
                        .Where(p => p.prestamo_id == s.entidad_id)
                        .SumAsync(p => (decimal?)p.interes_moratorio) ?? 0m;

                    if (moraActual <= 0)
                        return (false, "El crédito ya no tiene mora pendiente por condonar.");

                    return (true, "");
                }

                case TipoSolicitud.ELIMINAR_CREDITO:
                {
                    var prestamo = await _context.Prestamos.FindAsync(s.entidad_id);
                    if (prestamo == null)
                        return (false, $"El crédito #{s.entidad_id} ya no existe.");

                    // Un credito con pagos aplicados no se borra: hacerlo dejaria
                    // esos pagos apuntando a la nada y descuadraria la cartera.
                    var tienePagos = await _context.Pagos.AnyAsync(p => p.prestamo_id == s.entidad_id);
                    if (tienePagos)
                        return (false, "El crédito ya tiene pagos registrados; no puede eliminarse sin descuadrar la cartera.");

                    return (true, "");
                }

                case TipoSolicitud.ELIMINAR_CLIENTE:
                {
                    var cliente = await _context.Clientes.FindAsync(s.entidad_id);
                    if (cliente == null)
                        return (false, $"El cliente #{s.entidad_id} ya no existe.");

                    var creditosVivos = await _context.Prestamos.CountAsync(p =>
                        p.cliente_id == s.entidad_id && p.estatus != EstatusPrestamo.LIQUIDADO);

                    if (creditosVivos > 0)
                        return (false, $"El cliente tiene {creditosVivos} crédito(s) sin liquidar.");

                    return (true, "");
                }

                case TipoSolicitud.DAR_BAJA_TRABAJADOR:
                {
                    var trabajador = await _context.Usuarios.FindAsync(s.entidad_id);
                    if (trabajador == null)
                        return (false, $"El usuario #{s.entidad_id} ya no existe.");

                    if (trabajador.rol == RolUsuario.ADMIN)
                        return (false, "No se puede dar de baja a un administrador por esta vía.");

                    if (trabajador.estado == EstadoUsuario.INACTIVO)
                        return (false, "El trabajador ya está inactivo.");

                    return (true, "");
                }

                default:
                    // Los tipos aun no conectados no deben aprobarse por accidente.
                    return (false, $"El tipo de solicitud {s.tipo} todavía no está habilitado.");
            }
        }

        // =================================================================
        // EJECUCIÓN
        // =================================================================
        private async Task<ResultadoResolucion> EjecutarAsync(SolicitudAprobacion s, int adminId)
        {
            switch (s.tipo)
            {
                case TipoSolicitud.ELIMINAR_PAGO:
                    return await EjecutarEliminarPagoAsync(s, adminId);

                case TipoSolicitud.CONDONAR_MORA:
                    return await EjecutarCondonarMoraAsync(s, adminId);

                case TipoSolicitud.ELIMINAR_CREDITO:
                    return await EjecutarEliminarCreditoAsync(s, adminId);

                case TipoSolicitud.ELIMINAR_CLIENTE:
                    return await EjecutarEliminarClienteAsync(s, adminId);

                case TipoSolicitud.DAR_BAJA_TRABAJADOR:
                    return await EjecutarDarBajaTrabajadorAsync(s, adminId);

                default:
                    return new ResultadoResolucion(false,
                        $"El tipo {s.tipo} todavía no está habilitado.", EstadoSolicitud.PENDIENTE);
            }
        }

        private async Task<ResultadoResolucion> EjecutarEliminarCreditoAsync(SolicitudAprobacion s, int adminId)
        {
            var prestamo = await _context.Prestamos.FindAsync(s.entidad_id);
            if (prestamo == null)
                return new ResultadoResolucion(false, "El crédito ya no existe.", EstadoSolicitud.OBSOLETA);

            var periodos = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == s.entidad_id).ToListAsync();

            _context.PeriodosAmortizacion.RemoveRange(periodos);
            _context.Prestamos.Remove(prestamo);
            await _context.SaveChangesAsync();

            await _activityService.CreateActivity(
                ActivityType.CUSTOM,
                prestamo.cliente_id,
                prestamo.monto_total,
                NotificationLevel.HIGH,
                $"Crédito #{s.entidad_id} eliminado junto con sus {periodos.Count} periodo(s). " +
                $"Motivo: {s.justificacion} " +
                $"Autorizado por ADMIN vía solicitud #{s.id} (pidió el usuario #{s.solicitante_id}).",
                adminId
            );

            return new ResultadoResolucion(true,
                $"Crédito #{s.entidad_id} eliminado.", EstadoSolicitud.APROBADA);
        }

        private async Task<ResultadoResolucion> EjecutarEliminarClienteAsync(SolicitudAprobacion s, int adminId)
        {
            var cliente = await _context.Clientes.FindAsync(s.entidad_id);
            if (cliente == null)
                return new ResultadoResolucion(false, "El cliente ya no existe.", EstadoSolicitud.OBSOLETA);

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            await _activityService.CreateActivity(
                ActivityType.CUSTOM,
                s.entidad_id,
                0m,
                NotificationLevel.HIGH,
                $"Cliente #{s.entidad_id} eliminado. Motivo: {s.justificacion} " +
                $"Autorizado por ADMIN vía solicitud #{s.id} (pidió el usuario #{s.solicitante_id}).",
                adminId
            );

            return new ResultadoResolucion(true,
                $"Cliente #{s.entidad_id} eliminado.", EstadoSolicitud.APROBADA);
        }

        /// <summary>
        /// Baja de un trabajador: se marca INACTIVO, no se borra. Los pagos
        /// guardan cobrador_id y las actividades UserId; borrar el registro
        /// dejaria sin autor el historial de cobranza.
        /// </summary>
        private async Task<ResultadoResolucion> EjecutarDarBajaTrabajadorAsync(SolicitudAprobacion s, int adminId)
        {
            var trabajador = await _context.Usuarios.FindAsync(s.entidad_id);
            if (trabajador == null)
                return new ResultadoResolucion(false, "El usuario ya no existe.", EstadoSolicitud.OBSOLETA);

            if (trabajador.rol == RolUsuario.ADMIN)
                return new ResultadoResolucion(false,
                    "No se puede dar de baja a un administrador por esta vía.", EstadoSolicitud.OBSOLETA);

            trabajador.estado = EstadoUsuario.INACTIVO;
            await _context.SaveChangesAsync();

            await _activityService.CreateActivity(
                ActivityType.CUSTOM,
                0,
                0m,
                NotificationLevel.HIGH,
                $"Trabajador {trabajador.nombre} {trabajador.apellido} (#{s.entidad_id}, {trabajador.rol}) " +
                $"dado de baja. Motivo: {s.justificacion} " +
                $"Autorizado por ADMIN vía solicitud #{s.id} (pidió el usuario #{s.solicitante_id}).",
                adminId
            );

            return new ResultadoResolucion(true,
                $"{trabajador.nombre} {trabajador.apellido} quedó INACTIVO. Su historial se conserva.",
                EstadoSolicitud.APROBADA);
        }

        /// <summary>
        /// Misma secuencia que DELETE /api/Pago/{id}: capturar los datos antes
        /// de borrar, eliminar, reconstruir el prestamo y dejar rastro.
        /// </summary>
        private async Task<ResultadoResolucion> EjecutarEliminarPagoAsync(SolicitudAprobacion s, int adminId)
        {
            var pago = await _context.Pagos.FindAsync(s.entidad_id);
            if (pago == null)
                return new ResultadoResolucion(false, "El pago ya no existe.", EstadoSolicitud.OBSOLETA);

            var prestamoId  = pago.prestamo_id;
            var montoPagado = pago.monto_pagado;
            var fechaPago   = pago.fecha_pago;
            var metodoPago  = pago.metodo_pago;

            var clienteId = await _context.Prestamos
                .Where(p => p.prestamo_id == prestamoId)
                .Select(p => p.cliente_id)
                .FirstOrDefaultAsync();

            _context.Pagos.Remove(pago);
            await _context.SaveChangesAsync();

            await _motorRecalculo.Reconstruir(prestamoId);

            await _activityService.CreateActivity(
                ActivityType.PAYMENT_DELETED,
                clienteId,
                montoPagado,
                NotificationLevel.HIGH,
                $"Pago #{s.entidad_id} eliminado del crédito #{prestamoId} — {montoPagado:C2} " +
                $"({metodoPago ?? "EFECTIVO"}, fecha original {fechaPago:yyyy-MM-dd}). " +
                $"Autorizado por ADMIN vía solicitud #{s.id} (pidió el usuario #{s.solicitante_id}).",
                adminId
            );

            return new ResultadoResolucion(true,
                $"Pago #{s.entidad_id} eliminado y crédito #{prestamoId} recalculado.",
                EstadoSolicitud.APROBADA);
        }

        // MONEYPINE-FIX: TipoSolicitud.CONDONAR_MORA ya no se puede crear (ver
        // LimitesAutorizacion.PuedeSolicitar — quedó restringido a que solo un
        // ADMIN la ejecute directo vía PrestamoController.CondonarMora). Este
        // método sigue aquí únicamente para resolver limpio cualquier solicitud
        // que haya quedado PENDIENTE de antes del cambio; ya no se generan nuevas.
        // Condona en mora_condonada (durable) en vez de zerar interes_moratorio
        // directo, por la misma razón que PrestamoController.CondonarMora: ese
        // campo se recalcula desde cero en cada Reconstruir().
        private async Task<ResultadoResolucion> EjecutarCondonarMoraAsync(SolicitudAprobacion s, int adminId)
        {
            // MONEYPINE-FIX: refrescar interes_moratorio contra la mora real de HOY antes de
            // leerla — esta solicitud pudo quedar PENDIENTE varios días y el valor almacenado
            // podría estar desactualizado (mismo criterio que ya usa PrestamoController.CondonarMora).
            await _motorRecalculo.Reconstruir(s.entidad_id);

            var periodosConMora = await _context.PeriodosAmortizacion
                .Where(p => p.prestamo_id == s.entidad_id && (p.interes_moratorio > 0 || p.dias_moratorio > 0))
                .ToListAsync();

            if (periodosConMora.Count == 0)
                return new ResultadoResolucion(false,
                    "El crédito ya no tiene mora pendiente.", EstadoSolicitud.OBSOLETA);

            var montoCondonado = periodosConMora.Sum(p => p.interes_moratorio);

            foreach (var periodo in periodosConMora)
                periodo.mora_condonada += periodo.interes_moratorio;

            await _context.SaveChangesAsync();
            await _motorRecalculo.Reconstruir(s.entidad_id);

            await _activityService.CreateActivity(
                ActivityType.MORA_CONDONADA,
                s.cliente_id ?? 0,
                montoCondonado,
                NotificationLevel.NEUTRAL,
                $"Mora condonada en crédito #{s.entidad_id}: {montoCondonado:C2} " +
                $"({periodosConMora.Count} periodo(s)). Motivo: {s.justificacion} " +
                $"Autorizado por ADMIN vía solicitud #{s.id} (pidió el usuario #{s.solicitante_id}).",
                adminId
            );

            return new ResultadoResolucion(true,
                $"Mora condonada: {montoCondonado:C2} en {periodosConMora.Count} periodo(s).",
                EstadoSolicitud.APROBADA);
        }
    }
}
