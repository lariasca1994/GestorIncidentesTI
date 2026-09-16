using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlaService _slaService;

    public IndexModel(ApplicationDbContext db, ISlaService slaService)
    {
        _db = db;
        _slaService = slaService;
    }

    public List<Incidente> IncidentesAbiertos { get; set; } = new();
    public int TotalAbiertos { get; set; }
    public int TotalConSlaIncumplido { get; set; }
    public double PorcentajeCumplimiento { get; set; }

    public async Task OnGetAsync()
    {
        IncidentesAbiertos = await _db.Incidentes
            .Where(i => i.Estado != EstadoIncidente.Resuelto && i.Estado != EstadoIncidente.Cerrado)
            .OrderBy(i => i.FechaLimiteSla)
            .ToListAsync();

        TotalAbiertos = IncidentesAbiertos.Count;
        TotalConSlaIncumplido = await _db.Incidentes.CountAsync(i => i.SlaIncumplido);
        PorcentajeCumplimiento = await _slaService.CalcularCumplimientoAsync();
    }
}
