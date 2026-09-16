namespace GestorIncidentesTI.Models;

/// <summary>
/// Define el tiempo máximo de resolución (en horas) permitido según la prioridad.
/// Esto es lo que el servicio de SLA usa para calcular la fecha límite de cada incidente
/// y decidir cuándo debe escalar automáticamente.
/// </summary>
public class SlaDefinicion
{
    public int Id { get; set; }

    public PrioridadIncidente Prioridad { get; set; }

    /// <summary>Horas máximas para la primera respuesta.</summary>
    public int HorasRespuesta { get; set; }

    /// <summary>Horas máximas para la resolución completa.</summary>
    public int HorasResolucion { get; set; }

    /// <summary>
    /// Porcentaje del tiempo de resolución transcurrido a partir del cual
    /// se dispara el escalamiento automático (ej. 0.8 = escala al 80% del tiempo).
    /// </summary>
    public double UmbralEscalamiento { get; set; } = 0.8;
}
