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

    public List<Incidente> IncidentesAbiertos { get; set; } = new();
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

        IncidentesAbiertos = await query
            .Where(i => i.Estado != EstadoIncidente.Resuelto && i.Estado != EstadoIncidente.Cerrado)
            .OrderBy(i => i.FechaLimiteSla)
            .ToListAsync();

        TotalAbiertos = IncidentesAbiertos.Count;
        TotalConSlaIncumplido = await query.CountAsync(i => i.SlaIncumplido);
        PorcentajeCumplimiento = await _slaService.CalcularCumplimientoAsync(query);
    }
}
