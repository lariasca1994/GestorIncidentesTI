namespace GestorIncidentesTI.Models;

public record CrearIncidenteDto(
    string Titulo,
    string Descripcion,
    string Categoria,
    PrioridadIncidente Prioridad,
    string SolicitanteNombre
);

public record CambiarEstadoDto(
    EstadoIncidente NuevoEstado,
    string? Comentario
);

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
