using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Pages.Admin.Proyectos;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Proyecto> Proyectos { get; set; } = new();

    public async Task OnGetAsync()
    {
        Proyectos = await _db.Proyectos
            .OrderBy(p => p.Nombre)
            .ToListAsync();
    }
}