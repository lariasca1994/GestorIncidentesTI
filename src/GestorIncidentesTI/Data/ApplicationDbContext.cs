using GestorIncidentesTI.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Incidente> Incidentes => Set<Incidente>();
    public DbSet<SlaDefinicion> SlaDefiniciones => Set<SlaDefinicion>();
    public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<EventoSeguridad> EventosSeguridad => Set<EventoSeguridad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // obligatorio, primero — crea las tablas de Identity (AspNetUsers, AspNetRoles, etc.)

        modelBuilder.Entity<EventoSeguridad>(e =>
        {
            e.Property(x => x.Tipo).HasMaxLength(40);
            e.Property(x => x.UsuarioId).HasMaxLength(450);
            e.HasIndex(x => x.Fecha);
            e.HasIndex(x => x.UsuarioId);
        });

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

        // Columnas nuevas con largo acotado: no reservan nvarchar(max).
        modelBuilder.Entity<Incidente>()
            .Property(i => i.Impacto)
            .HasConversion<string>()
            .HasMaxLength(10);

        modelBuilder.Entity<Incidente>()
            .Property(i => i.Urgencia)
            .HasConversion<string>()
            .HasMaxLength(10);

        modelBuilder.Entity<Incidente>()
            .Property(i => i.Subcategoria)
            .HasMaxLength(60);

        modelBuilder.Entity<Proyecto>()
            .HasMany(p => p.Incidentes)
            .WithOne(i => i.Proyecto)
            .HasForeignKey(i => i.ProyectoId)
            .OnDelete(DeleteBehavior.Restrict); // evita borrar un proyecto que ya tiene incidentes

        // Datos semilla de SLA por prioridad — valores de ejemplo, ajustables.
        modelBuilder.Entity<SlaDefinicion>().HasData(
            new SlaDefinicion { Id = 1, Prioridad = PrioridadIncidente.Critica, HorasRespuesta = 1, HorasResolucion = 4, UmbralEscalamiento = 0.7 },
            new SlaDefinicion { Id = 2, Prioridad = PrioridadIncidente.Alta, HorasRespuesta = 2, HorasResolucion = 8, UmbralEscalamiento = 0.75 },
            new SlaDefinicion { Id = 3, Prioridad = PrioridadIncidente.Media, HorasRespuesta = 4, HorasResolucion = 24, UmbralEscalamiento = 0.8 },
            new SlaDefinicion { Id = 4, Prioridad = PrioridadIncidente.Baja, HorasRespuesta = 8, HorasResolucion = 72, UmbralEscalamiento = 0.85 }
        );
    }
}