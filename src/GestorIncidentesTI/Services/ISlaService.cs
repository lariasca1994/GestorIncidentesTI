using GestorIncidentesTI.Models;

namespace GestorIncidentesTI.Services;

public interface ISlaService
{
    /// <summary>Calcula la fecha límite de SLA para un incidente recién creado.</summary>
    Task<DateTime> CalcularFechaLimiteAsync(PrioridadIncidente prioridad, DateTime fechaCreacion);

    /// <summary>
    /// Revisa todos los incidentes abiertos/en progreso y escala los que ya
    /// superaron el umbral de tiempo definido en su SLA. Pensado para correr
    /// periódicamente (ej. un BackgroundService o un job programado).
    /// </summary>
    Task<int> EvaluarEscalamientosAsync();

    /// <summary>Calcula el % de cumplimiento de SLA sobre los incidentes resueltos/cerrados.</summary>
    Task<double> CalcularCumplimientoAsync();
    Task<double> CalcularCumplimientoAsync(IQueryable<Incidente> incidentes);
}
