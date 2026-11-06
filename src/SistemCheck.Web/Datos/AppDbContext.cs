using Microsoft.EntityFrameworkCore;

namespace SistemCheck.Web.Datos;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<InformeCacheado> InformesCacheados => Set<InformeCacheado>();
    public DbSet<Consulta> Consultas => Set<Consulta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MySQL no puede indexar columnas de texto sin largo, así que las claves y los índices lo declaran.
        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.Property(u => u.Email).HasMaxLength(254);
            usuario.Property(u => u.Empresa).HasMaxLength(100);
            usuario.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<InformeCacheado>(informe =>
        {
            informe.HasKey(i => i.Cuit);
            informe.Property(i => i.Cuit).HasMaxLength(11);
        });

        modelBuilder.Entity<Consulta>(consulta =>
        {
            consulta.Property(c => c.Cuit).HasMaxLength(11);
            consulta.Property(c => c.Denominacion).HasMaxLength(200);
            consulta.Property(c => c.DeudaTotal).HasPrecision(18, 2);
            consulta.HasIndex(c => new { c.UsuarioId, c.FechaUtc });
            consulta.HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(c => c.UsuarioId);
        });
    }
}
