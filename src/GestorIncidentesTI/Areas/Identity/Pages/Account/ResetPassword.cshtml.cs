using System.ComponentModel.DataAnnotations;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account;

/// <summary>
/// Paso 2 de "Olvidé mi contraseña": desde el enlace del correo, solo la contraseña nueva y su
/// confirmación (la cuenta la dice el enlace). Al guardarla se cierran las sesiones abiertas y
/// se muestran los códigos de recuperación nuevos (los anteriores quedan anulados).
/// </summary>
public class ResetPasswordModel : PageModel
{
    private readonly RecuperacionClave _recuperacion;

    public ResetPasswordModel(RecuperacionClave recuperacion) => _recuperacion = recuperacion;

    [BindProperty(SupportsGet = true)]
    public string? Usuario { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Codigo { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Escribe la contraseña nueva.")]
    [StringLength(100, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Confirma la contraseña nueva.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = "";

    public bool EnlaceValido { get; private set; }
    public bool Cambiada { get; private set; }

    /// <summary>Códigos de recuperación nuevos (los anteriores quedaron anulados).</summary>
    public IReadOnlyList<string> CodigosRecuperacion { get; private set; } = Array.Empty<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        EnlaceValido = await _recuperacion.ValidarAsync(Usuario, Codigo) is not null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            EnlaceValido = await _recuperacion.ValidarAsync(Usuario, Codigo) is not null;
            return Page();
        }

        var cambio = await _recuperacion.RestablecerAsync(Usuario, Codigo, Password);
        if (cambio is null) return Page(); // venció o ya se usó

        EnlaceValido = true;
        if (!cambio.Resultado.Succeeded)
        {
            foreach (var error in cambio.Resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await HttpContext.RequestServices.GetRequiredService<Auditoria>().RegistrarAsync(Auditoria.ClaveCambiada, cambio.Usuario);
        CodigosRecuperacion = cambio.CodigosRecuperacion;
        Cambiada = true;
        return Page();
    }
}
