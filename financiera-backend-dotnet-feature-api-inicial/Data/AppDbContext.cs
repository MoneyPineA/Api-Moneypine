using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Models;
using ApiEjemplo.Enums;
using ApiEjemplo.Tenancy;

namespace ApiEjemplo.Data
{
    public class AppDbContext : DbContext
    {
        // MONEYPINE-MT: ITenantContext es scoped (uno por request). AppDbContext
        // también se registra con AddDbContext normal (I6 — nunca AddDbContextPool),
        // así que cada instancia de contexto recibe el ITenantContext correcto de
        // SU request. Guardar la referencia aquí es seguro: no se comparte entre
        // requests.
        private readonly ITenantContext _tenant;

        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
            : base(options)
        {
            _tenant = tenant;
        }

        // MONEYPINE-MT: propiedades de INSTANCIA — NO constantes. El query filter
        // de abajo las referencia directamente (nunca Expression.Constant(_tenant)).
        // EF Core detecta el acceso a una propiedad de instancia del propio DbContext
        // y lo parametriza en cada consulta; si en cambio capturáramos el valor de
        // _tenant como constante, EF construye el modelo UNA vez (cacheado por tipo
        // de DbContext) y todos los requests siguientes verían el tenant del primero
        // — fuga de datos entre clientes, silenciosa. Invariante I2 del contrato.
        public int TenantId => _tenant.PrestamistaId;
        public bool EsPlataforma => _tenant.EsPlataforma;

        // MONEYPINE-MT: tabla de tenants (Fase 1 — Parte 4.2). El query filter
        // global que usa TenantId/EsPlataforma lo agrega mt-core-tenancy.
        public DbSet<Prestamista> Prestamistas { get; set; }

        // DbSets (tablas)
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Prestamo> Prestamos { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Documento> Documentos { get; set; } 
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<SolicitudAprobacion> SolicitudesAprobacion { get; set; }
        public DbSet<Grupo> Grupos { get; set; }
        public DbSet<Gerencia> Gerencias { get; set; }
        public DbSet<Ruta> Rutas { get; set; }
        public DbSet<GestionCobranza> GestionesCobranza { get; set; }
        public DbSet<NotificacionAgendada> NotificacionesAgendadas { get; set; }
        public DbSet<PeriodoAmortizacion> PeriodosAmortizacion { get; set; } // MONEYPINE-FIX: tabla amortización legada
        public DbSet<ProductoCredito> ProductosCredito { get; set; }

        // Módulo Ahorro
        public DbSet<ProductoAhorro>    ProductosAhorro    { get; set; }
        public DbSet<CuentaAhorro>      CuentasAhorro      { get; set; }
        public DbSet<MovimientoAhorro>  MovimientosAhorro  { get; set; }

        // MONEYPINE-FIX: biblioteca de conceptos del sistema (gastos/ingresos dinámicos)
        public DbSet<ConceptoSistema>   ConceptosSistema   { get; set; }

        // MONEYPINE-FIX: exclusiones del buró de crédito (persistencia cross-admin)
        public DbSet<BuroExclusion>     BuroExclusiones    { get; set; }

        // MONEYPINE-FIX: avales por préstamo
        public DbSet<PrestamoAval>      PrestamosAvales    { get; set; }

        // MONEYPINE-FIX: clientes auto-reportados al buró por mora >= 90 días
        public DbSet<BuroAutoReporte>   BuroAutoReportes   { get; set; }

        // Detalle granular por periodo de cada pago aplicado
        public DbSet<PagoDetalle>       PagoDetalles       { get; set; }

        // Lista negra persistida en BD (criterio: mora > 130 días y > $1500)
        public DbSet<ListaNegra>        ListasNegras       { get; set; }

        // Gastos del banco (Contabilidad/Rendimientos — "Gastos Recientes")
        public DbSet<GastoReciente>     GastosRecientes    { get; set; }

        // Anotaciones de cliente (pestaña Anotaciones del detalle)
        public DbSet<ClienteAnotacion>  ClienteAnotaciones { get; set; }

        // Formatos de documentos (plantillas oficiales, archivo en BLOB)
        public DbSet<FormatoDocumento>  FormatosDocumentos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =======================
            // FORMATO_DOCUMENTO
            // =======================
            modelBuilder.Entity<FormatoDocumento>(entity =>
            {
                entity.Property(f => f.contenido).HasColumnType("LONGBLOB");
                entity.Property(f => f.icono).HasColumnType("MEDIUMBLOB");
                entity.HasOne(f => f.Usuario)
                      .WithMany()
                      .HasForeignKey(f => f.usuario_id)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // =======================
            // CLIENTE_ANOTACION
            // =======================
            modelBuilder.Entity<ClienteAnotacion>(entity =>
            {
                entity.HasOne(a => a.Cliente)
                      .WithMany()
                      .HasForeignKey(a => a.cliente_id)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Usuario)
                      .WithMany()
                      .HasForeignKey(a => a.usuario_id)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // =======================
            // USUARIO
            // =======================
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(u => u.rol)
                      .HasConversion<string>()
                      .IsRequired();

                entity.Property(u => u.estado)
                      .HasConversion<string>()
                      .HasDefaultValue(EstadoUsuario.ACTIVO)
                      .IsRequired();

            });

            // =======================
            // PRESTAMO
            // =======================
            modelBuilder.Entity<Prestamo>(entity =>
            {
                entity.Property(p => p.estatus)
                      .HasConversion<string>()
                      .HasDefaultValue(EstatusPrestamo.ACTIVO)
                      .IsRequired();

                // MONEYPINE-FIX: Railway almacena forma_pago como MySQL enum (string), no int
                entity.Property(p => p.forma_pago)
                      .HasConversion<string>()
                      .IsRequired();
            });

            // =======================
            // GRUPO
            // =======================
            modelBuilder.Entity<Grupo>(entity =>
            {
                // MONEYPINE-FIX: Railway almacena forma_pago como MySQL enum (string), no int
                entity.Property(g => g.forma_pago)
                      .HasConversion<string>()
                      .IsRequired();
            });

            // =======================
            // PAGO
            // =======================
            modelBuilder.Entity<Pago>(entity =>
            {
                entity.Property(p => p.estatus)
                      .HasConversion<string>()
                      .HasDefaultValue(EstatusPago.APLICADO)
                      .IsRequired();
            });

            // =======================
            // CLIENTE -> USUARIO (1 a 1)
            // =======================
            modelBuilder.Entity<Cliente>()
                .HasOne(c => c.Usuario)
                .WithOne()
                .HasForeignKey<Cliente>(c => c.usuario_id)
                .OnDelete(DeleteBehavior.Cascade);

            // =======================
            // CLIENTE -> PRESTAMO (1 a muchos)
            // =======================
            modelBuilder.Entity<Prestamo>()
                .HasOne(p => p.Cliente)
                .WithMany(c => c.Prestamos)
                .HasForeignKey(p => p.cliente_id)
                .OnDelete(DeleteBehavior.Cascade);

            // =======================
            // GRUPO -> PRESTAMO (1 a muchos)
            // =======================
            modelBuilder.Entity<Prestamo>()
                .HasOne(p => p.Grupo)
                .WithMany(g => g.Prestamos)
                .HasForeignKey(p => p.grupo_id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // =======================
            // GERENCIA -> GERENTE (muchos a 1, nullable)
            // =======================
            modelBuilder.Entity<Gerencia>()
                .HasOne(g => g.Gerente)
                .WithMany()
                .HasForeignKey(g => g.gerente_id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // =======================
            // PRESTAMO -> PAGO (1 a muchos)
            // =======================
            modelBuilder.Entity<Pago>()
                .HasOne(p => p.Prestamo)
                .WithMany(pr => pr.Pagos)
                .HasForeignKey(p => p.prestamo_id)
                .OnDelete(DeleteBehavior.Cascade);

            // =======================
            // PRESTAMO -> GESTIONES (1 a muchos)
            // =======================
            modelBuilder.Entity<GestionCobranza>()
                .HasOne(g => g.Prestamo)
                .WithMany()
                .HasForeignKey(g => g.prestamo_id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GestionCobranza>()
                .HasOne(g => g.Gestor)
                .WithMany()
                .HasForeignKey(g => g.usuario_id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // =======================
            // PRESTAMO -> PERIODOS AMORTIZACION (1 a muchos)
            // =======================
            modelBuilder.Entity<PeriodoAmortizacion>()
                .HasOne(pa => pa.Prestamo)
                .WithMany()
                .HasForeignKey(pa => pa.prestamo_id)
                .OnDelete(DeleteBehavior.Cascade);

            // =======================
            // PRESTAMO -> NOTIFICACIONES AGENDADAS (1 a muchos)
            // =======================
            modelBuilder.Entity<NotificacionAgendada>()
                .HasOne(n => n.Prestamo)
                .WithMany()
                .HasForeignKey(n => n.prestamo_id)
                .OnDelete(DeleteBehavior.Cascade);

            // MONEYPINE-FIX: Railway usa tabla 'notifications' (minúsculas), no 'Notifications'
            modelBuilder.Entity<Notification>().ToTable("notifications");

            // =======================
            // SOLICITUDES DE APROBACIÓN
            // =======================
            modelBuilder.Entity<SolicitudAprobacion>(entity =>
            {
                entity.ToTable("solicitud_aprobacion");

                // Enums como texto: agregar un TipoSolicitud nuevo no debe
                // reinterpretar los registros ya guardados.
                entity.Property(s => s.tipo)
                      .HasConversion<string>()
                      .HasMaxLength(30)
                      .IsRequired();

                entity.Property(s => s.estado)
                      .HasConversion<string>()
                      .HasMaxLength(20)
                      .HasDefaultValue(EstadoSolicitud.PENDIENTE)
                      .IsRequired();

                entity.Property(s => s.justificacion).HasMaxLength(1000).IsRequired();
                entity.Property(s => s.respuesta).HasMaxLength(1000);
                entity.Property(s => s.descripcion).HasMaxLength(500);

                // La bandeja del admin filtra por estado y ordena por fecha.
                entity.HasIndex(s => new { s.estado, s.created_at });
                entity.HasIndex(s => s.solicitante_id);

                // Si se borra el usuario no se pierde el rastro de la solicitud.
                entity.HasOne(s => s.Solicitante)
                      .WithMany()
                      .HasForeignKey(s => s.solicitante_id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Resolutor)
                      .WithMany()
                      .HasForeignKey(s => s.resuelta_por)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // =======================
            // MÓDULO AHORRO
            // =======================
            modelBuilder.Entity<CuentaAhorro>(entity =>
            {
                entity.Property(c => c.estatus)
                      .HasConversion<string>()
                      .HasDefaultValue(EstatusAhorro.ACTIVA)
                      .IsRequired();
            });

            // Se guarda como texto (VISTA / PLAZO_FIJO) para que la columna se
            // lea sola al consultar la BD, igual que estatus.
            modelBuilder.Entity<ProductoAhorro>(entity =>
            {
                entity.Property(p => p.tipo)
                      .HasConversion<string>()
                      .HasMaxLength(20)
                      .HasDefaultValue(TipoProductoAhorro.VISTA)
                      .IsRequired();
            });

            modelBuilder.Entity<CuentaAhorro>()
                .HasOne(c => c.Cliente)
                .WithMany()
                .HasForeignKey(c => c.cliente_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CuentaAhorro>()
                .HasOne(c => c.Producto)
                .WithMany(p => p.Cuentas)
                .HasForeignKey(c => c.producto_ahorro_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoAhorro>()
                .HasOne(m => m.Cuenta)
                .WithMany(c => c.Movimientos)
                .HasForeignKey(m => m.cuenta_ahorro_id)
                .OnDelete(DeleteBehavior.Cascade);

            // Description y UserId añadidos a Railway con ALTER TABLE (2026-05-24)

            // =======================
            // PRESTAMO AVAL
            // =======================
            // MONEYPINE-FIX: relación prestamo → avales (clientes garantes)
            modelBuilder.Entity<PrestamoAval>()
                .HasOne(a => a.Prestamo)
                .WithMany()
                .HasForeignKey(a => a.prestamo_id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PrestamoAval>()
                .HasOne(a => a.Aval)
                .WithMany()
                .HasForeignKey(a => a.cliente_id_aval)
                .OnDelete(DeleteBehavior.Restrict);

            // =======================
            // PAGO DETALLE
            // =======================
            modelBuilder.Entity<PagoDetalle>()
                .HasOne(pd => pd.Pago)
                .WithMany()
                .HasForeignKey(pd => pd.pago_id)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (no Cascade) — evita doble cascada MySQL. Prestamo → Pago → PagoDetalle
            // ya maneja la eliminación en cadena; esta FK es solo para integridad referencial.
            modelBuilder.Entity<PagoDetalle>()
                .HasOne(pd => pd.Prestamo)
                .WithMany()
                .HasForeignKey(pd => pd.prestamo_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PagoDetalle>()
                .HasOne(pd => pd.Periodo)
                .WithMany()
                .HasForeignKey(pd => pd.periodo_id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // =======================
            // LISTA NEGRA
            // =======================
            modelBuilder.Entity<ListaNegra>(entity =>
            {
                entity.HasIndex(ln => ln.cliente_id);
                entity.HasIndex(ln => ln.prestamo_id);
                entity.HasIndex(ln => ln.estado);
                entity.HasIndex(ln => new { ln.cliente_id, ln.prestamo_id, ln.estado });

                entity.HasOne(ln => ln.Cliente)
                      .WithMany()
                      .HasForeignKey(ln => ln.cliente_id)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ln => ln.Prestamo)
                      .WithMany()
                      .HasForeignKey(ln => ln.prestamo_id)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // =======================
            // MONEYPINE-MT: PRESTAMISTA (tenant) — Fase 1, Parte 4.2
            // =======================
            modelBuilder.Entity<Prestamista>(entity =>
            {
                entity.Property(p => p.estatus)
                      .HasConversion<string>()
                      .HasDefaultValue(EstatusPrestamista.ACTIVO)
                      .IsRequired();

                entity.HasIndex(p => p.slug).IsUnique();
            });

            // =======================
            // MONEYPINE-MT: FK prestamista_id -> prestamista para TODA entidad
            // que implemente ITenantEntity (Fase 1 — Parte 4.3). Se hace por
            // reflexión sobre el modelo en vez de repetir HasOne/WithMany 27
            // veces. Restrict: borrar un tenant no debe cascadear en silencio
            // sobre miles de filas de negocio de otros módulos.
            // El query filter global (HasQueryFilter) NO se agrega aquí —
            // es responsabilidad de mt-core-tenancy.
            // =======================
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasOne(typeof(Prestamista))
                        .WithMany()
                        .HasForeignKey(nameof(ITenantEntity.prestamista_id))
                        .OnDelete(DeleteBehavior.Restrict);
                }
            }

            // =======================
            // MONEYPINE-MT: índices de tenant — Fase 1, Parte 4.3
            // El tenant SIEMPRE va primero en el índice.
            // =======================

            // cliente no tiene columna 'estatus' propia (vive en usuario, 1 a 1)
            // — el documento de arquitectura lo asume por error. Índice simple.
            modelBuilder.Entity<Cliente>()
                .HasIndex(c => c.prestamista_id)
                .HasDatabaseName("ix_cliente_tenant");

            modelBuilder.Entity<Cliente>()
                .HasIndex(c => new { c.prestamista_id, c.clave_cliente })
                .IsUnique()
                .HasDatabaseName("ux_cliente_clave");

            modelBuilder.Entity<Prestamo>()
                .HasIndex(p => new { p.prestamista_id, p.estatus, p.fecha_proximo_pago })
                .HasDatabaseName("ix_prestamo_tenant");

            modelBuilder.Entity<Pago>()
                .HasIndex(p => new { p.prestamista_id, p.fecha_pago })
                .HasDatabaseName("ix_pago_tenant");

            modelBuilder.Entity<Gerencia>()
                .HasIndex(g => new { g.prestamista_id, g.codigo })
                .IsUnique()
                .HasDatabaseName("ux_gerencia_codigo");

            // MONEYPINE-MT: NO únicos — hay codigo='C.M' duplicado en ruta
            // (ruta_id 3 y 5) y correo='' duplicado 6 veces en usuario, ambos
            // dentro del mismo tenant 1. Un UNIQUE tronaría al aplicar la
            // migración. Queda como índice normal; limpiar los duplicados y
            // subir a UNIQUE es deuda para un slice de datos aparte.
            modelBuilder.Entity<Ruta>()
                .HasIndex(r => new { r.prestamista_id, r.codigo })
                .HasDatabaseName("ix_ruta_codigo");

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => new { u.prestamista_id, u.correo })
                .HasDatabaseName("ix_usuario_correo");

            // =======================
            // MONEYPINE-MT: Global Query Filter de tenant — Fase 1, Parte 4.5.
            // Por reflexión sobre las mismas 27 entidades ITenantEntity que ya
            // recorrió el arquitecto para las FKs (Data/AppDbContext.cs arriba).
            // AplicarFiltroTenant<T> referencia TenantId/EsPlataforma — propiedades
            // de instancia — nunca _tenant directo. Ver invariante I2.
            // =======================
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                         .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType)))
            {
                _setFilterMethod.MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }

        private static readonly MethodInfo _setFilterMethod = typeof(AppDbContext)
            .GetMethod(nameof(AplicarFiltroTenant), BindingFlags.NonPublic | BindingFlags.Instance)!;

        // MONEYPINE-MT: e.prestamista_id == TenantId (propiedad de instancia, NO
        // Expression.Constant(_tenant)) — es lo único que evita el bug de caché
        // del modelo de EF Core. Ver invariante I2 arriba.
        private void AplicarFiltroTenant<T>(ModelBuilder mb) where T : class, ITenantEntity
            => mb.Entity<T>().HasQueryFilter(e => EsPlataforma || e.prestamista_id == TenantId);

        // =======================
        // MONEYPINE-MT: asignación y protección automática de tenant — Fase 1,
        // Parte 4.5. Override de AMBOS SaveChanges (sync y async): el proyecto
        // tiene código síncrono existente que llama SaveChanges() a secas, y si
        // solo interceptamos la versión async esos flujos siguen creando filas
        // sin prestamista_id.
        //   - Added -> se FUERZA el tenant actual, se ignora lo que traiga la
        //     entidad.
        //   - Modified/Deleted de OTRO tenant (y no plataforma) -> se bloquea:
        //     evita que un request ya autenticado como tenant A edite/borre una
        //     fila de tenant B que haya llegado al ChangeTracker por otra vía
        //     (p.ej. FindAsync sin filtrar — ver trampa documentada en el reporte).
        //
        // MONEYPINE-MT: la asignación en Added NO se condiciona a
        // `prestamista_id == 0`. Con esa condición, un body que trajera un
        // prestamista_id ajeno se respetaba tal cual: se demostró insertando una
        // fila del tenant 999 con un token del tenant 1 (POST /api/ConceptoSistema
        // con prestamista_id en el JSON -> 200 OK). El origen del tenant es el
        // token, nunca el cuerpo de la petición, así que se sobrescribe siempre.
        // Esto cubre de una vez a todo controlador que haga [FromBody] Entidad +
        // Add(dto), sin depender de que cada uno se acuerde de limpiar el campo.
        // =======================
        private void AplicarTenantAEntidadesRastreadas()
        {
            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.State == EntityState.Added && !EsPlataforma)
                    entry.Entity.prestamista_id = TenantId;

                if ((entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                    && !EsPlataforma && entry.Entity.prestamista_id != TenantId)
                    throw new UnauthorizedAccessException("Escritura cross-tenant bloqueada");
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AplicarTenantAEntidadesRastreadas();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AplicarTenantAEntidadesRastreadas();
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}