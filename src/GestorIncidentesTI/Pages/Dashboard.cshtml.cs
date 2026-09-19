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

        var abiertos = await query
            .Where(i => i.Estado != EstadoIncidente.Resuelto && i.Estado != EstadoIncidente.Cerrado)
            .OrderBy(i => i.FechaLimiteSla)
            .ToListAsync();

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
            else if (ahora > i.FechaLimiteSla.Value)
            {
                nivelRiesgo = "vencido";
                tiempoTexto = $"Vencido hace {FormatearTiempo(ahora - i.FechaLimiteSla.Value)}";
            }
            else
            {
                var totalVentana = (i.FechaLimiteSla.Value - i.FechaCreacion).TotalHours;
                var transcurrido = (ahora - i.FechaCreacion).TotalHours;
                var fraccion = totalVentana > 0 ? transcurrido / totalVentana : 1;
                var umbral = umbrales.TryGetValue(i.Prioridad, out var u) ? u : 0.8;

                nivelRiesgo = fraccion >= umbral ? "riesgo" : "ok";
                tiempoTexto = $"Quedan {FormatearTiempo(i.FechaLimiteSla.Value - ahora)}";
            }

            return new IncidenteDashboardVm(i.Id, i.Titulo, i.Prioridad, i.Estado, i.NivelActual, tiempoTexto, nivelRiesgo);
        }).ToList();

        TotalAbiertos = IncidentesAbiertos.Count;
        TotalConSlaIncumplido = await query.CountAsync(i => i.SlaIncumplido)
            + IncidentesAbiertos.Count(v => v.NivelRiesgo == "vencido");
        PorcentajeCumplimiento = await _slaService.CalcularCumplimientoAsync(query);
    }

    private static string FormatearTiempo(TimeSpan ts)
    {
        var abs = ts.Duration();
        return $"{(int)abs.TotalHours}h {abs.Minutes}m";
    }
}