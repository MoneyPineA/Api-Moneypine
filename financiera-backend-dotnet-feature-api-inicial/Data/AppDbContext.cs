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
        }
    }
}