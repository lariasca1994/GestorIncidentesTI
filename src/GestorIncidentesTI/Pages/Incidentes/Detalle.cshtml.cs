using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestorIncidentesTI.Pages.Incidentes;

[Authorize]
public class DetalleModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetalleModel(ApplicationDbContext db)
    {
        _db = db;
    }

    private static readonly Dictionary<EstadoIncidente, EstadoIncidente[]> Transiciones = new()
    {
        [EstadoIncidente.Abierto] = new[] { EstadoIncidente.EnProgreso, EstadoIncidente.Escalado, EstadoIncidente.Resuelto },
        [EstadoIncidente.EnProgreso] = new[] { EstadoIncidente.Escalado, EstadoIncidente.Resuelto },
        [EstadoIncidente.Escalado] = new[] { EstadoIncidente.EnProgreso, EstadoIncidente.Resuelto },
        [EstadoIncidente.Resuelto] = new[] { EstadoIncidente.Cerrado, EstadoIncidente.EnProgreso },
        [EstadoIncidente.Cerrado] = Array.Empty<EstadoIncidente>()
    };

    public Incidente Incidente { get; set; } = null!;
    public List<EstadoIncidente> EstadosDisponibles { get; set; } = new();

    [BindProperty]
    public string? NuevoComentario { get; set; }

    [BindProperty]
    public EstadoIncidente? NuevoEstado { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var incidente = await CargarConAlcanceAsync(id);
        if (incidente is null) return NotFound();

        Incidente = incidente;
        EstadosDisponibles = Transiciones.TryGetValue(incidente.Estado, out var opciones) ? opciones.ToList() : new();
        return Page();
    }

    public async Task<IActionResult> OnPostComentarioAsync(int id)
    {
        var incidente = await CargarConAlcanceAsync(id);
        if (incidente is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(NuevoComentario))
        {
            _db.HistorialEstados.Add(new HistorialEstado
            {
                IncidenteId = incidente.Id,
                EstadoAnterior = incidente.Estado,
                EstadoNuevo = incidente.Estado,
                NivelAnterior = incidente.NivelActual,
                NivelNuevo = incidente.NivelActual,
                Comentario = NuevoComentario,
                ModificadoPorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToPage("./Detalle", new { id });
    }

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id)
    {
        var incidente = await CargarConAlcanceAsync(id);
        if (incidente is null) return NotFound();

        if (NuevoEstado is null ||
            !Transiciones.TryGetValue(incidente.Estado, out var opciones) ||
            !opciones.Contains(NuevoEstado.Value))
        {
            ModelState.AddModelError(string.Empty, "Esa transición de estado no está permitida.");
            Incidente = incidente;
            EstadosDisponibles = Transiciones.TryGetValue(incidente.Estado, out var op2) ? op2.ToList() : new();
            return Page();
        }

        var estadoAnterior = incidente.Estado;
        incidente.Estado = NuevoEstado.Value;

        if (incidente.Estado is EstadoIncidente.Resuelto or EstadoIncidente.Cerrado)
        {
            incidente.FechaResolucion = DateTime.UtcNow;
            if (incidente.FechaLimiteSla is not null)
                incidente.SlaIncumplido = incidente.FechaResolucion > incidente.FechaLimiteSla;
        }

        _db.HistorialEstados.Add(new HistorialEstado
        {
            IncidenteId = incidente.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = incidente.Estado,
            NivelAnterior = incidente.NivelActual,
            NivelNuevo = incidente.NivelActual,
            Comentario = NuevoComentario,
            ModificadoPorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("./Detalle", new { id });
    }

    private async Task<Incidente?> CargarConAlcanceAsync(int id)
    {
        var query = _db.Incidentes.Include(i => i.Proyecto).Include(i => i.Historial).AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var proyectoId = await _db.Users.Where(u => u.Id == userId)
                .Select(u => u.ProyectoId).SingleOrDefaultAsync();
            query = proyectoId is null ? query.Where(_ => false) : query.Where(i => i.ProyectoId == proyectoId.Value);
        }

        return await query.FirstOrDefaultAsync(i => i.Id == id);
    }
}