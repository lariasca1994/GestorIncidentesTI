using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GestorIncidentesTI.Pages.Incidentes;

[Authorize]
public class CrearModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISlaService _slaService;

    public CrearModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ISlaService slaService)
    {
        _db = db;
        _userManager = userManager;
        _slaService = slaService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EsAdmin { get; set; }
    public SelectList? ProyectosDisponibles { get; set; }

    public class InputModel
    {
        [Required, StringLength(160)]
        public string Titulo { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Descripcion { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Categoria { get; set; } = string.Empty;

        [Required]
        public PrioridadIncidente Prioridad { get; set; }

        [Required, StringLength(160)]
        public string SolicitanteNombre { get; set; } = string.Empty;

        public int? ProyectoId { get; set; }
    }

    public async Task OnGetAsync(int? proyectoId)
    {
        EsAdmin = User.IsInRole("Admin");
        if (EsAdmin)
        {
            ProyectosDisponibles = new SelectList(await _db.Proyectos.OrderBy(p => p.Nombre).ToListAsync(), "Id", "Nombre");
            Input.ProyectoId = proyectoId;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        EsAdmin = User.IsInRole("Admin");
        if (EsAdmin)
        {
            ProyectosDisponibles = new SelectList(await _db.Proyectos.OrderBy(p => p.Nombre).ToListAsync(), "Id", "Nombre");
        }

        if (!ModelState.IsValid) return Page();

        var usuario = await _userManager.GetUserAsync(User);
        int proyectoIdFinal;

        if (EsAdmin)
        {
            if (Input.ProyectoId is null)
            {
                ModelState.AddModelError(nameof(Input.ProyectoId), "Selecciona un proyecto.");
                return Page();
            }
            proyectoIdFinal = Input.ProyectoId.Value;
        }
        else
        {
            if (usuario?.ProyectoId is null)
            {
                ModelState.AddModelError(string.Empty, "Tu cuenta no tiene un proyecto asignado.");
                return Page();
            }
            proyectoIdFinal = usuario.ProyectoId.Value;
        }

        var ahora = DateTime.UtcNow;
        var fechaLimite = await _slaService.CalcularFechaLimiteAsync(Input.Prioridad, ahora);

        var incidente = new Incidente
        {
            Titulo = Input.Titulo,
            Descripcion = Input.Descripcion,
            Categoria = Input.Categoria,
            Prioridad = Input.Prioridad,
            SolicitanteNombre = Input.SolicitanteNombre,
            FechaCreacion = ahora,
            FechaLimiteSla = fechaLimite,
            Estado = EstadoIncidente.Abierto,
            NivelActual = NivelEscalamiento.N1,
            ProyectoId = proyectoIdFinal,
            CreadoPorUserId = usuario?.Id
        };

        incidente.Historial.Add(new HistorialEstado
        {
            EstadoAnterior = EstadoIncidente.Abierto,
            EstadoNuevo = EstadoIncidente.Abierto,
            NivelAnterior = NivelEscalamiento.N1,
            NivelNuevo = NivelEscalamiento.N1,
            Comentario = "Incidente creado.",
            ModificadoPorUserId = usuario?.Id
        });

        _db.Incidentes.Add(incidente);
        await _db.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}