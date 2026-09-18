using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
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

    public CrearModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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

        return RedirectToPage("./Index");
    }
}