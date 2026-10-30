namespace GestorIncidentesTI.Models;

public enum PrioridadIncidente
{
    Baja = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}

public enum EstadoIncidente
{
    Abierto,
    EnProgreso,
    Escalado,
    Resuelto,
    Cerrado,
    // Esperando información del solicitante: el reloj del SLA se detiene.
    EnEspera
}

public enum NivelEscalamiento
{
    N1 = 1,
    N2 = 2,
    N3 = 3
}

// Impacto × urgencia definen la prioridad (matriz ITIL), ver MatrizPrioridad.
public enum ImpactoIncidente
{
    Bajo = 1,
    Medio = 2,
    Alto = 3
}

public enum UrgenciaIncidente
{
    Baja = 1,
    Media = 2,
    Alta = 3
}
