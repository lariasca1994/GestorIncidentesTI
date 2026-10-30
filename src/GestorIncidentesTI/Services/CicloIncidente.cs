using GestorIncidentesTI.Models;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Reglas de estado de un incidente, compartidas por las páginas y la API:
/// transiciones permitidas, primera respuesta, pausa del SLA en espera del
/// solicitante y fecha de resolución.
/// </summary>
public static class CicloIncidente
{
    public static readonly IReadOnlyDictionary<EstadoIncidente, EstadoIncidente[]> Transiciones =
        new Dictionary<EstadoIncidente, EstadoIncidente[]>
        {
            [EstadoIncidente.Abierto] = new[] { EstadoIncidente.EnProgreso, EstadoIncidente.EnEspera, EstadoIncidente.Escalado, EstadoIncidente.Resuelto },
            [EstadoIncidente.EnProgreso] = new[] { EstadoIncidente.EnEspera, EstadoIncidente.Escalado, EstadoIncidente.Resuelto },
            [EstadoIncidente.Escalado] = new[] { EstadoIncidente.EnProgreso, EstadoIncidente.EnEspera, EstadoIncidente.Resuelto },
            [EstadoIncidente.EnEspera] = new[] { EstadoIncidente.EnProgreso, EstadoIncidente.Resuelto },
            [EstadoIncidente.Resuelto] = new[] { EstadoIncidente.Cerrado, EstadoIncidente.EnProgreso },
            [EstadoIncidente.Cerrado] = Array.Empty<EstadoIncidente>()
        };

    public static IReadOnlyList<EstadoIncidente> Siguientes(EstadoIncidente actual) =>
        Transiciones.TryGetValue(actual, out var opciones) ? opciones : Array.Empty<EstadoIncidente>();

    public static bool EsTransicionValida(EstadoIncidente actual, EstadoIncidente nuevo) =>
        Siguientes(actual).Contains(nuevo);

    /// <summary>Estados en los que el reloj del SLA corre.</summary>
    public static bool SlaCorriendo(EstadoIncidente estado) =>
        estado is EstadoIncidente.Abierto or EstadoIncidente.EnProgreso or EstadoIncidente.Escalado;

    /// <summary>Aplica un cambio de estado ya validado.</summary>
    public static void CambiarEstado(Incidente incidente, EstadoIncidente nuevo, DateTime ahora)
    {
        // Al salir de la espera, el tiempo detenido se suma a la fecha límite.
        if (incidente.Estado == EstadoIncidente.EnEspera && incidente.InicioEspera is { } inicioEspera)
        {
            var pausa = CalendarioSla.HorasEntre(inicioEspera, ahora, incidente.Prioridad);
            incidente.HorasEnEspera += pausa;
            if (incidente.FechaLimiteSla is { } limite && pausa > 0)
                incidente.FechaLimiteSla = CalendarioSla.SumarHoras(limite, pausa, incidente.Prioridad);
            incidente.InicioEspera = null;
        }

        incidente.Estado = nuevo;
        RegistrarPrimeraRespuesta(incidente, ahora);

        if (nuevo == EstadoIncidente.EnEspera)
            incidente.InicioEspera = ahora;

        // La resolución fija el cumplimiento del SLA. Cerrar después un caso
        // resuelto a tiempo no lo vuelve incumplido.
        if (nuevo == EstadoIncidente.Resuelto || (nuevo == EstadoIncidente.Cerrado && incidente.FechaResolucion is null))
        {
            incidente.FechaResolucion = ahora;
            if (incidente.FechaLimiteSla is not null)
                incidente.SlaIncumplido = ahora > incidente.FechaLimiteSla;
        }
        else if (nuevo == EstadoIncidente.EnProgreso)
        {
            incidente.FechaResolucion = null; // reabierto
        }
    }

    /// <summary>La primera acción del equipo sobre el incidente cierra el SLA de respuesta.</summary>
    public static void RegistrarPrimeraRespuesta(Incidente incidente, DateTime ahora)
    {
        if (incidente.FechaPrimeraRespuesta is not null) return;

        incidente.FechaPrimeraRespuesta = ahora;
        if (incidente.FechaLimiteRespuesta is { } limite)
            incidente.RespuestaIncumplida = ahora > limite;
    }

    /// <summary>Fracción del tiempo de resolución consumida (0 a 1+), sin contar la espera.</summary>
    public static double FraccionSlaConsumida(Incidente incidente, DateTime ahora)
    {
        if (incidente.FechaLimiteSla is not { } limite) return 0;

        var total = CalendarioSla.HorasEntre(incidente.FechaCreacion, limite, incidente.Prioridad) - incidente.HorasEnEspera;
        if (total <= 0) return ahora >= limite ? 1 : 0;

        var hasta = incidente.Estado == EstadoIncidente.EnEspera && incidente.InicioEspera is { } inicio ? inicio : ahora;
        var transcurrido = CalendarioSla.HorasEntre(incidente.FechaCreacion, hasta, incidente.Prioridad) - incidente.HorasEnEspera;
        return Math.Max(0, transcurrido) / total;
    }
}
