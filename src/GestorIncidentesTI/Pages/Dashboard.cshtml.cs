using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestorIncidentesTI.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlaService _slaService;

    public DashboardModel(ApplicationDbContext db, ISlaService slaService)
    {
        _db = db;
        _slaService = slaService;
    }

    public record IncidenteDashboardVm(
        int Id,
        string Titulo,
        PrioridadIncidente Prioridad,
        EstadoIncidente Estado,
        NivelEscalamiento NivelActual,
        string TiempoSlaTexto,
        string NivelRiesgo // "ok" | "riesgo" | "vencido"
    );

    public List<IncidenteDashboardVm> IncidentesAbiertos { get; set; } = new();
    public int TotalAbiertos { get; set; }
    public int TotalConSlaIncumplido { get; set; }
    public double PorcentajeCumplimiento { get; set; }

    public async Task OnGetAsync()
    {
        var query = _db.Incidentes.AsQueryable();
        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var proyectoId = await _db.Users.Where(u => u.Id == userId)
                .Select(u => u.ProyectoId).SingleOrDefaultAsync();
            query = proyectoId is null ? query.Where(_ => false) : query.Where(i => i.ProyectoId == proyectoId.Value);
        }

        var umbrales = await _db.SlaDefiniciones
            .ToDictionaryAsync(s => s.Prioridad, s => s.UmbralEscalamiento);

        // El estado se guarda como texto y algunos registros traen caracteres invisibles alrededor
        // (por ejemplo un salto de línea): en SQL 'Cerrado' + salto de línea no es igual a 'Cerrado',
        // pero .NET sí lo lee como Cerrado. Por eso el filtro por estado se aplica ya convertido.
        var todos = await query.OrderBy(i => i.FechaLimiteSla).ToListAsync();
        var abiertos = todos.Where(i => i.Estado is not (EstadoIncidente.Resuelto or EstadoIncidente.Cerrado)).ToList();

        var ahora = DateTime.UtcNow;

        IncidentesAbiertos = abiertos.Select(i =>
        {
            string nivelRiesgo;
            string tiempoTexto;

            if (i.FechaLimiteSla is null)
            {
                nivelRiesgo = "ok";
                tiempoTexto = "Sin SLA";
            }
            else if (i.Estado == EstadoIncidente.EnEspera)
            {
                nivelRiesgo = "ok";
                tiempoTexto = "SLA detenido (en espera)";
            }
            else if (ahora > i.FechaLimiteSla.Value)
            {
                nivelRiesgo = "vencido";
                tiempoTexto = $"Vencido hace {FormatearTiempo(ahora - i.FechaLimiteSla.Value)}";
            }
            else
            {
                // En horas de SLA: hábiles salvo Crítica, sin contar la espera.
                var fraccion = CicloIncidente.FraccionSlaConsumida(i, ahora);
                var umbral = umbrales.TryGetValue(i.Prioridad, out var u) ? u : 0.8;

                nivelRiesgo = fraccion >= umbral ? "riesgo" : "ok";
                tiempoTexto = $"Quedan {FormatearTiempo(i.FechaLimiteSla.Value - ahora)}";
            }

            return new IncidenteDashboardVm(i.Id, i.Titulo, i.Prioridad, i.Estado, i.NivelActual, tiempoTexto, nivelRiesgo);
        }).ToList();

        TotalAbiertos = IncidentesAbiertos.Count;
        // Cada incidente cuenta una vez: el escalamiento ya marca SlaIncumplido al vencer, así que
        // sumar además los abiertos vencidos los contaba dos veces.
        var vencidosAbiertos = IncidentesAbiertos.Where(v => v.NivelRiesgo == "vencido").Select(v => v.Id).ToHashSet();
        TotalConSlaIncumplido = todos.Count(i => i.SlaIncumplido || vencidosAbiertos.Contains(i.Id));
        PorcentajeCumplimiento = SlaService.CalcularCumplimiento(todos);
    }

    private static string FormatearTiempo(TimeSpan ts)
    {
        // Legible a cualquier escala: "57 d 21 h", "5 h 12 min" o "40 min".
        var abs = ts.Duration();
        if (abs.TotalDays >= 2) return $"{(int)abs.TotalDays} d {abs.Hours} h";
        if (abs.TotalHours >= 1) return $"{(int)abs.TotalHours} h {abs.Minutes} min";
        return $"{Math.Max(1, abs.Minutes)} min";
    }
}