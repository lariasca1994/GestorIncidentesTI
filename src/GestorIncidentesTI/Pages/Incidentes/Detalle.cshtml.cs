using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
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
    private readonly INotificador _notificador;

    public DetalleModel(ApplicationDbContext db, INotificador notificador)
    {
        _db = db;
        _notificador = notificador;
    }

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
        EstadosDisponibles = CicloIncidente.Siguientes(incidente.Estado).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostComentarioAsync(int id)
    {
        var incidente = await CargarConAlcanceAsync(id);
        if (incidente is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(NuevoComentario))
        {
            // El comentario de alguien distinto a quien lo reportó es la
            // primera respuesta del equipo.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != incidente.CreadoPorUserId)
                CicloIncidente.RegistrarPrimeraRespuesta(incidente, DateTime.UtcNow);

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

        if (NuevoEstado is null || !CicloIncidente.EsTransicionValida(incidente.Estado, NuevoEstado.Value))
        {
            ModelState.AddModelError(string.Empty, "Esa transición de estado no está permitida.");
            Incidente = incidente;
            EstadosDisponibles = CicloIncidente.Siguientes(incidente.Estado).ToList();
            return Page();
        }

        var estadoAnterior = incidente.Estado;
        CicloIncidente.CambiarEstado(incidente, NuevoEstado.Value, DateTime.UtcNow);

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

        if (incidente.Estado is EstadoIncidente.Resuelto or EstadoIncidente.Cerrado)
        {
            var actor = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var proyecto = await _db.Proyectos.Where(p => p.Id == incidente.ProyectoId).Select(p => p.Nombre).FirstOrDefaultAsync();
            var (asunto, aviso) = AvisosIncidente.Completado(incidente, proyecto, actor, NuevoComentario,
                Url.Page("/Incidentes/Detalle", null, new { id = incidente.Id }, "https"));
            await _notificador.NotificarAsync(actor, asunto, aviso);
        }

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