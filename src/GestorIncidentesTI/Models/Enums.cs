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
    Cerrado
}

public enum NivelEscalamiento
{
    N1 = 1,
    N2 = 2,
    N3 = 3
}
