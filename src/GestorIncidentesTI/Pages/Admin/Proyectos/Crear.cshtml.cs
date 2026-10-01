using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace GestorIncidentesTI.Pages.Admin.Proyectos;

[Authorize(Roles = "Admin")]
public class CrearModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificador _notificador;

    public CrearModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificador notificador)
    {
        _db = db;
        _userManager = userManager;
        _notificador = notificador;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [Display(Name = "Nombre del proyecto")]
        public string Nombre { get; set; } = string.Empty;

        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var proyecto = new Proyecto
        {
            Nombre = Input.Nombre,
            Descripcion = Input.Descripcion,
            CreadoPorUserId = _userManager.GetUserId(User)
        };

        _db.Proyectos.Add(proyecto);
        await _db.SaveChangesAsync();

        var actor = _userManager.GetUserName(User);
        var (asunto, aviso) = AvisosIncidente.ProyectoCreado(proyecto, actor,
            Url.Page("/Admin/Proyectos/Index", null, null, "https"));
        await _notificador.NotificarAsync(actor, asunto, aviso);

        return RedirectToPage("./Index");
    }
}