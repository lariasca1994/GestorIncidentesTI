using System.Security.Claims;
using GestorIncidentesTI.Data;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account;

/// <summary>
/// Segundo paso con un código de recuperación (ya se puso la contraseña). Cada código sirve
/// una vez; los errores cuentan para el bloqueo temporal, como los demás códigos.
/// </summary>
public class LoginWithRecoveryCodeModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DosPasos _dosPasos;

    public LoginWithRecoveryCodeModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, DosPasos dosPasos)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _dosPasos = dosPasos;
    }

    [BindProperty]
    public string? Codigo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await _signInManager.GetTwoFactorAuthenticationUserAsync() is null ? VolverAlIngreso() : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return VolverAlIngreso();

        if (await _userManager.IsLockedOutAsync(user))
        {
            ModelState.AddModelError(string.Empty, DosPasos.Mensaje(ResultadoCodigo.Bloqueado));
            return Page();
        }

        var auditoria = HttpContext.RequestServices.GetRequiredService<Auditoria>();
        var codigo = (Codigo ?? "").Replace(" ", "").Trim();
        var resultado = codigo.Length is > 0 and <= 20
            ? await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, codigo)
            : IdentityResult.Failed();
        if (!resultado.Succeeded)
        {
            await _userManager.AccessFailedAsync(user);
            await auditoria.RegistrarAsync(Auditoria.CodigoErrado, user);
            ModelState.AddModelError(string.Empty, await _userManager.IsLockedOutAsync(user)
                ? DosPasos.Mensaje(ResultadoCodigo.Bloqueado)
                : "El código de recuperación no es correcto o ya se usó.");
            return Page();
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await auditoria.RegistrarAsync(Auditoria.Ingreso, user);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        await _signInManager.SignInWithClaimsAsync(user, false,
            new[] { new Claim("amr", DosPasosObligatorioMiddleware.ClaimMfa) });

        var restantes = await _dosPasos.CodigosRecuperacionAsync(user);
        TempData["Mensaje"] = $"Ingresaste con un código de recuperación. Te quedan {restantes}; aquí puedes generar nuevos.";
        return RedirectToPage("./Seguridad");
    }

    private IActionResult VolverAlIngreso()
    {
        TempData["ErrorMessage"] = "La verificación venció. Ingresa de nuevo.";
        return RedirectToPage("./Login", new { ReturnUrl });
    }
}
