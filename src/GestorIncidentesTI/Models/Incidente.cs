namespace GestorIncidentesTI.Models;

public class Incidente
{
    public int Id { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    // Nullable: los incidentes anteriores al catálogo no tienen subcategoría.
    public string? Subcategoria { get; set; }

    // Impacto y urgencia definen la prioridad. Son nullable porque los
    // incidentes anteriores a la matriz se crearon con la prioridad a mano.
    public ImpactoIncidente? Impacto { get; set; }
    public UrgenciaIncidente? Urgencia { get; set; }
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

    /// <summary>Límite para la primera respuesta (cambio de estado o comentario del equipo).</summary>
    public DateTime? FechaLimiteRespuesta { get; set; }
    public DateTime? FechaPrimeraRespuesta { get; set; }
    public bool RespuestaIncumplida { get; set; }

    /// <summary>Desde cuándo está en espera del solicitante (SLA detenido).</summary>
    public DateTime? InicioEspera { get; set; }
    /// <summary>Horas de SLA acumuladas en espera; la fecha límite ya las incluye.</summary>
    public double HorasEnEspera { get; set; }

    public int ProyectoId { get; set; }
    public Proyecto? Proyecto { get; set; }

    public List<HistorialEstado> Historial { get; set; } = new();
}
