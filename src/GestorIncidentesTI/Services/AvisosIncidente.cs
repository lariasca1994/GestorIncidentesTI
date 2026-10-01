using GestorIncidentesTI.Models;

namespace GestorIncidentesTI.Services;

/// <summary>Arma los avisos por correo de incidentes y proyectos.</summary>
public static class AvisosIncidente
{
    private static readonly TimeZoneInfo ZonaBogota = BuscarZonaBogota();

    public static string Estado(EstadoIncidente estado) => estado switch
    {
        EstadoIncidente.EnProgreso => "En progreso",
        _ => estado.ToString()
    };

    public static string Prioridad(PrioridadIncidente prioridad) => prioridad switch
    {
        PrioridadIncidente.Critica => "Crítica",
        _ => prioridad.ToString()
    };

    public static (string Asunto, Aviso Aviso) Creado(Incidente incidente, string? proyecto, string? actor, string? enlace)
    {
        var detalles = Base(incidente, proyecto);
        if (incidente.FechaLimiteSla is { } limite)
            detalles.Add(("Fecha límite SLA", HoraBogota(limite)));
        detalles.Add(("Creado por", actor ?? "—"));
        detalles.Add(("Fecha de creación", HoraBogota(incidente.FechaCreacion)));

        var notas = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(incidente.Descripcion))
            notas.Add(("Descripción registrada por el usuario", incidente.Descripcion));

        var aviso = new Aviso(
            "Nuevo incidente registrado",
            $"#{incidente.Id} · {incidente.Titulo}",
            new[] { ChipEstado(incidente.Estado), ChipPrioridad(incidente.Prioridad), ChipNeutro($"Nivel {incidente.NivelActual}") },
            detalles, notas, enlace, "Ver incidente");

        return ($"[Gestor TI] Nuevo incidente #{incidente.Id} · {Prioridad(incidente.Prioridad)} · {incidente.Titulo}", aviso);
    }

    public static (string Asunto, Aviso Aviso) Completado(Incidente incidente, string? proyecto, string? actor, string? comentario, string? enlace)
    {
        var estado = Estado(incidente.Estado);
        var detalles = Base(incidente, proyecto);
        if (incidente.FechaLimiteSla is { } limite)
            detalles.Add(("Fecha límite SLA", HoraBogota(limite)));
        if (incidente.FechaResolucion is { } resolucion)
            detalles.Add(($"Fecha de {(incidente.Estado == EstadoIncidente.Cerrado ? "cierre" : "resolución")}", HoraBogota(resolucion)));
        detalles.Add(("Gestionado por", actor ?? "—"));

        var notas = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(comentario))
            notas.Add(($"Comentario del usuario al marcarlo como {estado.ToLowerInvariant()}", comentario));
        if (!string.IsNullOrWhiteSpace(incidente.Descripcion))
            notas.Add(("Descripción original del incidente", incidente.Descripcion));

        var sla = incidente.SlaIncumplido
            ? new Chip("SLA incumplido", "#b91c1c", "#fee2e2")
            : new Chip("Dentro del SLA", "#15803d", "#dcfce7");

        var aviso = new Aviso(
            $"Incidente {estado.ToLowerInvariant()}",
            $"#{incidente.Id} · {incidente.Titulo}",
            new[] { ChipEstado(incidente.Estado), ChipPrioridad(incidente.Prioridad), sla },
            detalles, notas, enlace, "Ver incidente");

        return ($"[Gestor TI] Incidente #{incidente.Id} {estado.ToLowerInvariant()} · {incidente.Titulo}", aviso);
    }

    public static (string Asunto, Aviso Aviso) ProyectoCreado(Proyecto proyecto, string? actor, string? enlace)
    {
        var notas = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(proyecto.Descripcion))
            notas.Add(("Descripción registrada por el usuario", proyecto.Descripcion));

        var aviso = new Aviso(
            "Nuevo proyecto creado",
            proyecto.Nombre,
            new[] { ChipNeutro("Proyecto") },
            new List<(string, string)>
            {
                ("Proyecto", proyecto.Nombre),
                ("Creado por", actor ?? "—"),
                ("Fecha de creación", HoraBogota(proyecto.FechaCreacion))
            },
            notas, enlace, "Ver proyectos");

        return ($"[Gestor TI] Nuevo proyecto · {proyecto.Nombre}", aviso);
    }

    private static List<(string, string)> Base(Incidente incidente, string? proyecto) => new()
    {
        ("Proyecto", proyecto ?? "—"),
        ("Estado", Estado(incidente.Estado)),
        ("Prioridad", Prioridad(incidente.Prioridad)),
        ("Nivel de soporte", incidente.NivelActual.ToString()),
        ("Categoría", incidente.Categoria),
        ("Solicitante", incidente.SolicitanteNombre)
    };

    // Mismos tonos que los .badge de site.css, en colores sólidos (los
    // clientes de correo no siempre respetan rgba).
    private static Chip ChipEstado(EstadoIncidente estado) => estado switch
    {
        EstadoIncidente.Abierto => new Chip("Abierto", "#1d4ed8", "#dbeafe"),
        EstadoIncidente.EnProgreso => new Chip("En progreso", "#a16207", "#fef9c3"),
        EstadoIncidente.Escalado => new Chip("Escalado", "#c2410c", "#ffedd5"),
        EstadoIncidente.Resuelto => new Chip("Resuelto", "#15803d", "#dcfce7"),
        _ => new Chip(Estado(estado), "#45635d", "#e6f4f1")
    };

    private static Chip ChipPrioridad(PrioridadIncidente prioridad) => prioridad switch
    {
        PrioridadIncidente.Media => new Chip("Prioridad media", "#1d4ed8", "#dbeafe"),
        PrioridadIncidente.Alta => new Chip("Prioridad alta", "#c2410c", "#ffedd5"),
        PrioridadIncidente.Critica => new Chip("Prioridad crítica", "#b91c1c", "#fee2e2"),
        _ => new Chip("Prioridad baja", "#45635d", "#e6f4f1")
    };

    private static Chip ChipNeutro(string texto) => new(texto, "#0f766e", "#e6f4f1");

    private static string HoraBogota(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ZonaBogota).ToString("dd/MM/yyyy HH:mm") + " (hora Bogotá)";

    private static TimeZoneInfo BuscarZonaBogota()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Bogota", TimeSpan.FromHours(-5), "Bogotá", "Bogotá");
    }
}
