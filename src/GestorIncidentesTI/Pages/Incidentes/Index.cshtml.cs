using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestorIncidentesTI.Pages.Incidentes;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Incidente> Incidentes { get; set; } = new();
    public bool EsAdmin { get; set; }
    public int? ProyectoIdFiltro { get; set; }
    public string? NombreProyectoFiltro { get; set; }

    public async Task OnGetAsync(int? proyectoId, EstadoIncidente? estado)
    {
        EsAdmin = User.IsInRole("Admin");
        var query = _db.Incidentes.Include(i => i.Proyecto).AsQueryable();

        if (!EsAdmin)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var proyectoUsuario = await _db.Users.Where(u => u.Id == userId)
                .Select(u => u.ProyectoId).SingleOrDefaultAsync();
            query = proyectoUsuario is null
                ? query.Where(_ => false)
                : query.Where(i => i.ProyectoId == proyectoUsuario.Value);
        }
        else if (proyectoId is not null)
        {
            query = query.Where(i => i.ProyectoId == proyectoId.Value);
            ProyectoIdFiltro = proyectoId;
            NombreProyectoFiltro = await _db.Proyectos.Where(p => p.Id == proyectoId.Value)
                .Select(p => p.Nombre).SingleOrDefaultAsync();
        }

        if (estado is not null)
            query = query.Where(i => i.Estado == estado);

        Incidentes = await query.OrderByDescending(i => i.FechaCreacion).ToListAsync();
    }
}