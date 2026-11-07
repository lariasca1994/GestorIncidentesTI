using System.Security.Claims;
using GestorIncidentesTI.Data;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account;

/// <summary>
/// "Perdí mi autenticador", desde el segundo paso del ingreso (ya se puso la contraseña):
/// 1. se envía un código de 6 dígitos al correo de la cuenta, aunque el método correo no esté
///    activo; acertarlo NO abre la sesión;
/// 2. se configura la app nueva (QR) y se confirma con su primer código: solo entonces se
///    reemplaza la app anterior, se cierran las demás sesiones, se generan códigos de
///    recuperación nuevos (anulan los anteriores) y se abre la sesión.
/// </summary>
public class PerdiAutenticadorModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DosPasos _dosPasos;

    public PerdiAutenticadorModel(SignInManager<ApplicationUser> signInManager, DosPasos dosPasos)
    {
        _signInManager = signInManager;
        _dosPasos = dosPasos;
    }

    [BindProperty]
    public string? Codigo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Paso 2: el código del correo ya se verificó y falta activar la app nueva.</summary>
    public bool ConfigurandoApp { get; private set; }
    public string? Aviso { get; private set; }
    public string? QrSvg { get; private set; }
    public string? Clave { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await CargarAsync();
        if (user is null) return VolverAlIngreso();
        if (!ConfigurandoApp)
            Aviso = await _dosPasos.EnviarCodigoRecuperacionAsync(user)
                ? $"Enviamos un código a {user.Email}. Vence en 15 minutos."
                : "Ya enviamos 3 códigos en los últimos 15 minutos. Usa el último o espera un momento.";
        return Page();
    }

    public Task<IActionResult> OnPostReenviarAsync() => OnGetAsync();

    /// <summary>Paso 1: código del correo. Si es correcto pasa a configurar la app; no abre sesión.</summary>
    public async Task<IActionResult> OnPostVerificarAsync()
    {
        var user = await CargarAsync();
        if (user is null) return VolverAlIngreso();
        if (ConfigurandoApp) return RedirectToPage(new { ReturnUrl });

        var resultado = await _dosPasos.VerificarAsync(user, DosPasos.Recuperacion, Codigo);
        if (resultado != ResultadoCodigo.Correcto)
        {
            await HttpContext.RequestServices.GetRequiredService<Auditoria>().RegistrarAsync(Auditoria.CodigoErrado, user);
            ModelState.AddModelError(string.Empty, DosPasos.Mensaje(resultado));
            return Page();
        }

        await _dosPasos.MarcarRecuperacionAsync(user);
        return RedirectToPage(new { ReturnUrl });
    }

    /// <summary>Paso 2: primer código de la app nueva. Reemplaza la anterior y abre la sesión.</summary>
    public async Task<IActionResult> OnPostActivarAsync()
    {
        var user = await CargarAsync();
        if (user is null) return VolverAlIngreso();
        if (!ConfigurandoApp) return RedirectToPage(new { ReturnUrl });

        var auditoria = HttpContext.RequestServices.GetRequiredService<Auditoria>();
        var resultado = await _dosPasos.ReemplazarAppAsync(user, Codigo);
        if (resultado != ResultadoCodigo.Correcto)
        {
            await auditoria.RegistrarAsync(Auditoria.CodigoErrado, user);
            ModelState.AddModelError(string.Empty, resultado == ResultadoCodigo.Incorrecto
                ? "El código no es correcto. Revisa que la app muestre la cuenta nueva."
                : DosPasos.Mensaje(resultado));
            return Page();
        }

        var codigos = await _dosPasos.NuevosCodigosRecuperacionAsync(user);
        await auditoria.RegistrarAsync(Auditoria.MetodoActivado, user);
        await auditoria.RegistrarAsync(Auditoria.Ingreso, user);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        await _signInManager.SignInWithClaimsAsync(user, false,
            new[] { new Claim("amr", DosPasosObligatorioMiddleware.ClaimMfa) });

        TempData[SeguridadModel.ClaveCodigosNuevos] = string.Join(' ', codigos);
        TempData["Mensaje"] = "Tu nueva app autenticadora quedó activa; la anterior ya no sirve.";
        return RedirectToPage("./Seguridad");
    }

    private async Task<ApplicationUser?> CargarAsync()
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !(await _dosPasos.EstadoAsync(user)).Configurado) return null;

        ConfigurandoApp = await _dosPasos.RecuperacionVigenteAsync(user);
        if (ConfigurandoApp)
        {
            var clave = await _dosPasos.ClavePendienteAsync(user);
            QrSvg = DosPasos.QrSvg(user.Email!, clave);
            Clave = DosPasos.ClaveLegible(clave);
        }
        return user;
    }

    private IActionResult VolverAlIngreso()
    {
        TempData["ErrorMessage"] = "La verificación venció. Ingresa de nuevo.";
        return RedirectToPage("./Login", new { ReturnUrl });
    }
}
