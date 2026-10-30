using System.ComponentModel.DataAnnotations;

namespace GestorIncidentesTI.Models;

public sealed class CrearIncidenteDto
{
    [Required, StringLength(160)]
    public string Titulo { get; init; } = string.Empty;

    [Required, StringLength(4000)]
    public string Descripcion { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Categoria { get; init; } = string.Empty;

    [Required, StringLength(60)]
    public string Subcategoria { get; init; } = string.Empty;

    // La prioridad sale de impacto × urgencia. Prioridad directa solo se usa
    // si no llegan los dos (compatibilidad con clientes anteriores).
    public ImpactoIncidente? Impacto { get; init; }
    public UrgenciaIncidente? Urgencia { get; init; }
    public PrioridadIncidente? Prioridad { get; init; }

    [Required, StringLength(160)]
    public string SolicitanteNombre { get; init; } = string.Empty;

    // Solo un administrador puede indicar un proyecto distinto al suyo.
    [Range(1, int.MaxValue)]
    public int? ProyectoId { get; init; }
}

public sealed class CambiarEstadoDto
{
    [Required]
    public EstadoIncidente NuevoEstado { get; init; }

    [StringLength(1000)]
    public string? Comentario { get; init; }
}

public record IncidenteResumenDto(
    int Id,
    string Titulo,
    PrioridadIncidente Prioridad,
    EstadoIncidente Estado,
    NivelEscalamiento NivelActual,
    DateTime FechaCreacion,
    DateTime? FechaLimiteSla,
    bool SlaIncumplido
);
