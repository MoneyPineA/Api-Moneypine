using Microsoft.EntityFrameworkCore;
using ApiEjemplo.Models;
using ApiEjemplo.Enums;

namespace ApiEjemplo.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // DbSets (tablas)
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Prestamo> Prestamos { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Documento> Documentos { get; set; } 
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
            // MÓDULO AHORRO
            // =======================
            modelBuilder.Entity<CuentaAhorro>(entity =>
            {
                entity.Property(c => c.estatus)
                      .HasConversion<string>()
                      .HasDefaultValue(EstatusAhorro.ACTIVA)
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

            modelBuilder.Entity<PagoDetalle>()
                .HasOne(pd => pd.Prestamo)
                .WithMany()
                .HasForeignKey(pd => pd.prestamo_id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PagoDetalle>()
                .HasOne(pd => pd.Periodo)
                .WithMany()
                .HasForeignKey(pd => pd.periodo_id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        }
    }
}