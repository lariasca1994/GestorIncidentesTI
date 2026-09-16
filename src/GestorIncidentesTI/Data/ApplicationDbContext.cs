using GestorIncidentesTI.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Incidente> Incidentes => Set<Incidente>();
    public DbSet<SlaDefinicion> SlaDefiniciones => Set<SlaDefinicion>();
    public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incidente>()
            .HasMany(i => i.Historial)
            .WithOne(h => h.Incidente)
            .HasForeignKey(h => h.IncidenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Incidente>()
            .Property(i => i.Prioridad)
            .HasConversion<string>();

        modelBuilder.Entity<Incidente>()
            .Property(i => i.Estado)
            .HasConversion<string>();

        modelBuilder.Entity<Incidente>()
            .Property(i => i.NivelActual)
            .HasConversion<string>();

        // Datos semilla de SLA por prioridad — valores de ejemplo, ajustables.
        modelBuilder.Entity<SlaDefinicion>().HasData(
            new SlaDefinicion { Id = 1, Prioridad = PrioridadIncidente.Critica, HorasRespuesta = 1, HorasResolucion = 4, UmbralEscalamiento = 0.7 },
            new SlaDefinicion { Id = 2, Prioridad = PrioridadIncidente.Alta, HorasRespuesta = 2, HorasResolucion = 8, UmbralEscalamiento = 0.75 },
            new SlaDefinicion { Id = 3, Prioridad = PrioridadIncidente.Media, HorasRespuesta = 4, HorasResolucion = 24, UmbralEscalamiento = 0.8 },
            new SlaDefinicion { Id = 4, Prioridad = PrioridadIncidente.Baja, HorasRespuesta = 8, HorasResolucion = 72, UmbralEscalamiento = 0.85 }
        );
    }
}
