using System.Text.Json.Serialization;

namespace GestorIncidentesTI.Models;

/// <summary>
/// Registro de auditoría: cada vez que un incidente cambia de estado o de nivel
/// de escalamiento, se guarda una entrada aquí. Es la evidencia de trazabilidad
/// que un ITSM real necesita mostrar.
/// </summary>
public class HistorialEstado
{
    public int Id { get; set; }

    public int IncidenteId { get; set; }

    [JsonIgnore]
    public Incidente? Incidente { get; set; }

    public EstadoIncidente EstadoAnterior { get; set; }
    public EstadoIncidente EstadoNuevo { get; set; }

    public NivelEscalamiento NivelAnterior { get; set; }
    public NivelEscalamiento NivelNuevo { get; set; }

    public string? Comentario { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}