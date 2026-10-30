using GestorIncidentesTI.Models;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Matriz ITIL impacto × urgencia. Quien reporta indica a cuántos afecta y qué
/// tan rápido se necesita; la prioridad sale de aquí y no se elige a mano.
/// </summary>
public static class MatrizPrioridad
{
    public static PrioridadIncidente Calcular(ImpactoIncidente impacto, UrgenciaIncidente urgencia) =>
        ((int)impacto + (int)urgencia) switch
        {
            6 => PrioridadIncidente.Critica, // alto + alta
            5 => PrioridadIncidente.Alta,    // alto + media, medio + alta
            4 => PrioridadIncidente.Media,   // alto + baja, medio + media, bajo + alta
            _ => PrioridadIncidente.Baja     // medio + baja, bajo + media, bajo + baja
        };

    public static string Impacto(ImpactoIncidente impacto) => impacto switch
    {
        ImpactoIncidente.Alto => "Alto: toda el área o un servicio crítico",
        ImpactoIncidente.Medio => "Medio: varias personas o un equipo",
        _ => "Bajo: una sola persona"
    };

    public static string Urgencia(UrgenciaIncidente urgencia) => urgencia switch
    {
        UrgenciaIncidente.Alta => "Alta: el trabajo está detenido",
        UrgenciaIncidente.Media => "Media: hay una alternativa temporal",
        _ => "Baja: puede esperar"
    };
}
