using GestorIncidentesTI.Models;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Reloj del SLA. Las prioridades Crítica corren 24×7; el resto solo cuenta
/// horario hábil en Bogotá: lunes a viernes de 8:00 a 18:00, sin festivos de
/// Colombia. Así un incidente Medio creado el viernes a las 17:00 no vence el
/// sábado. Bogotá no tiene horario de verano: siempre es UTC−5.
/// </summary>
public static class CalendarioSla
{
    private static readonly TimeSpan DesfaseBogota = TimeSpan.FromHours(-5);
    private static readonly TimeSpan InicioJornada = TimeSpan.FromHours(8);
    private static readonly TimeSpan FinJornada = TimeSpan.FromHours(18);
    private static readonly Dictionary<int, HashSet<DateTime>> FestivosPorAnio = new();
    private static readonly object CandadoFestivos = new();

    public static bool Es24x7(PrioridadIncidente prioridad) => prioridad == PrioridadIncidente.Critica;

    public static string Descripcion(PrioridadIncidente prioridad) =>
        Es24x7(prioridad) ? "24×7" : "Horario hábil (L-V 8:00-18:00, sin festivos)";

    /// <summary>Suma horas de SLA a una fecha UTC y devuelve la fecha UTC resultante.</summary>
    public static DateTime SumarHoras(DateTime desdeUtc, double horas, PrioridadIncidente prioridad)
    {
        if (horas <= 0) return desdeUtc;
        if (Es24x7(prioridad)) return desdeUtc.AddHours(horas);

        var local = desdeUtc + DesfaseBogota;
        var restante = TimeSpan.FromHours(horas);

        while (true)
        {
            if (!EsDiaHabil(local.Date))
            {
                local = local.Date.AddDays(1) + InicioJornada;
                continue;
            }

            var inicio = local.Date + InicioJornada;
            var fin = local.Date + FinJornada;
            if (local < inicio) local = inicio;
            if (local >= fin)
            {
                local = local.Date.AddDays(1) + InicioJornada;
                continue;
            }

            var disponible = fin - local;
            if (restante <= disponible)
                return DateTime.SpecifyKind(local + restante - DesfaseBogota, DateTimeKind.Utc);

            restante -= disponible;
            local = local.Date.AddDays(1) + InicioJornada;
        }
    }

    /// <summary>Horas de SLA que corren entre dos fechas UTC.</summary>
    public static double HorasEntre(DateTime desdeUtc, DateTime hastaUtc, PrioridadIncidente prioridad)
    {
        if (hastaUtc <= desdeUtc) return 0;
        if (Es24x7(prioridad)) return (hastaUtc - desdeUtc).TotalHours;

        var desde = desdeUtc + DesfaseBogota;
        var hasta = hastaUtc + DesfaseBogota;
        var total = TimeSpan.Zero;

        for (var dia = desde.Date; dia <= hasta.Date; dia = dia.AddDays(1))
        {
            if (!EsDiaHabil(dia)) continue;

            var inicio = Max(dia + InicioJornada, desde);
            var fin = Min(dia + FinJornada, hasta);
            if (fin > inicio) total += fin - inicio;
        }

        return total.TotalHours;
    }

    public static bool EsDiaHabil(DateTime dia) =>
        dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Festivos(dia.Year).Contains(dia.Date);

    /// <summary>Festivos de Colombia (Ley 51 de 1983, "ley Emiliani").</summary>
    public static HashSet<DateTime> Festivos(int anio)
    {
        lock (CandadoFestivos)
        {
            if (FestivosPorAnio.TryGetValue(anio, out var cache)) return cache;

            var pascua = DomingoDePascua(anio);
            var festivos = new HashSet<DateTime>
            {
                // Fijos
                new(anio, 1, 1), new(anio, 5, 1), new(anio, 7, 20),
                new(anio, 8, 7), new(anio, 12, 8), new(anio, 12, 25),
                // Se trasladan al lunes siguiente
                AlLunes(new(anio, 1, 6)), AlLunes(new(anio, 3, 19)), AlLunes(new(anio, 6, 29)),
                AlLunes(new(anio, 8, 15)), AlLunes(new(anio, 10, 12)), AlLunes(new(anio, 11, 1)),
                AlLunes(new(anio, 11, 11)),
                // Según la Pascua
                pascua.AddDays(-3), pascua.AddDays(-2),
                pascua.AddDays(43), pascua.AddDays(64), pascua.AddDays(71)
            };

            FestivosPorAnio[anio] = festivos;
            return festivos;
        }
    }

    private static DateTime AlLunes(DateTime fecha) =>
        fecha.DayOfWeek == DayOfWeek.Monday ? fecha : fecha.AddDays(((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7);

    // Algoritmo anónimo gregoriano (Meeus/Jones/Butcher).
    private static DateTime DomingoDePascua(int anio)
    {
        int a = anio % 19, b = anio / 100, c = anio % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31, dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(anio, mes, dia);
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
