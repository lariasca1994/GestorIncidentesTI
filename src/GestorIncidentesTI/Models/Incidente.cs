namespace GestorIncidentesTI.Models;

public class Incidente
{
    public int Id { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;

    public PrioridadIncidente Prioridad { get; set; }
    public EstadoIncidente Estado { get; set; } = EstadoIncidente.Abierto;
    public NivelEscalamiento NivelActual { get; set; } = NivelEscalamiento.N1;

    public string SolicitanteNombre { get; set; } = string.Empty;
    public string? AsignadoA { get; set; }
    // Nullable para conservar registros históricos creados antes de la auditoría.
    public string? CreadoPorUserId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaLimiteSla { get; set; }
    public DateTime? FechaResolucion { get; set; }

    /// <summary>true si el incidente superó su fecha límite de SLA sin resolverse.</summary>
    public bool SlaIncumplido { get; set; }

    public int ProyectoId { get; set; }
    public Proyecto? Proyecto { get; set; }

    public List<HistorialEstado> Historial { get; set; } = new();
}
