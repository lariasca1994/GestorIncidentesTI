using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GestorIncidentesTI.Data;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Cambio de contraseña desde Seguridad. Al guardarla se cierran las demás sesiones
/// (SecurityStamp), esta sigue abierta y se generan códigos de recuperación nuevos que
/// anulan los anteriores; se muestran una sola vez en Seguridad.
/// </summary>
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DosPasos _dosPasos;

    public ChangePasswordModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, DosPasos dosPasos)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dosPasos = dosPasos;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Escribe tu contraseña actual.")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = "";

        [Required(ErrorMessage = "Escribe la contraseña nueva.")]
        [StringLength(100, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Confirma la contraseña nueva.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user is null ? Challenge() : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var resultado = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await HttpContext.RequestServices.GetRequiredService<Auditoria>().RegistrarAsync(Auditoria.ClaveCambiada, user);
        var codigos = await _dosPasos.NuevosCodigosRecuperacionAsync(user);

        // ChangePasswordAsync renovó el SecurityStamp: se vuelve a firmar esta sesión, con su
        // segundo paso y su "Recordarme", para que solo se cierren las demás.
        var actual = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        await _signInManager.SignInWithClaimsAsync(user, actual.Properties?.IsPersistent ?? false,
            new[] { new Claim("amr", DosPasosObligatorioMiddleware.ClaimMfa) });

        TempData[SeguridadModel.ClaveCodigosNuevos] = string.Join(' ', codigos);
        TempData["Mensaje"] = "Tu contraseña cambió y se cerraron las demás sesiones.";
        return RedirectToPage("/Account/Seguridad");
    }
}
