using System.Security.Claims;
using GestorIncidentesTI.Data;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account;

/// <summary>
/// Verificación en dos pasos de la cuenta. Sin método configurado es la única página
/// disponible (primer ingreso o recién registrado): se elige app autenticadora o correo y se
/// confirma con el primer código. Con método, permite agregar el que falta y elegir el preferido.
/// </summary>
[Authorize]
public class SeguridadModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DosPasos _dosPasos;

    public SeguridadModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, DosPasos dosPasos)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dosPasos = dosPasos;
    }

    [BindProperty(SupportsGet = true)]
    public string? Metodo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? Codigo { get; set; }

    public EstadoDosPasos Estado { get; private set; } = new(false, false, null);
    public string? Email { get; private set; }
    public string? QrSvg { get; private set; }
    public string? Clave { get; private set; }
    public string? Aviso { get; private set; }

    [TempData]
    public string? Mensaje { get; set; }

    /// <summary>Primer ingreso: todavía no hay método.</summary>
    public bool Eligiendo => !Estado.Configurado;

    public async Task<IActionResult> OnGetAsync()
    {
        var (user, salida) = await CargarAsync();
        if (user is null) return salida!;

        if (Metodo == DosPasos.Correo)
            Aviso = await _dosPasos.EnviarCodigoAsync(user)
                ? $"Enviamos un código a {user.Email}. Vence en 10 minutos."
                : "Ya enviamos 3 códigos en los últimos 10 minutos. Usa el último o espera un momento.";
        return Page();
    }

    public async Task<IActionResult> OnPostReenviarAsync()
    {
        Metodo = DosPasos.Correo;
        return await OnGetAsync();
    }

    public async Task<IActionResult> OnPostActivarAsync()
    {
        var (user, salida) = await CargarAsync();
        if (user is null) return salida!;
        if (Metodo is null) return RedirectToPage();

        var resultado = await _dosPasos.VerificarAsync(user, Metodo, Codigo);
        if (resultado != ResultadoCodigo.Correcto)
        {
            ModelState.AddModelError(string.Empty, DosPasos.Mensaje(resultado));
            return Page();
        }

        var primerMetodo = Eligiendo;
        await _dosPasos.ActivarAsync(user, Metodo);
        await HttpContext.RequestServices.GetRequiredService<Auditoria>().RegistrarAsync(Auditoria.MetodoActivado, user);
        var nombre = Metodo == DosPasos.App ? "La app autenticadora" : "El código por correo";
        if (primerMetodo)
        {
            // El código recién confirmado es el segundo paso de esta sesión.
            await _signInManager.SignInWithClaimsAsync(user, false,
                new[] { new Claim("amr", DosPasosObligatorioMiddleware.ClaimMfa) });
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
        }

        Mensaje = $"{nombre} quedó activo.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPreferidoAsync()
    {
        var (user, salida) = await CargarAsync();
        if (user is null) return salida!;
        if (Metodo is not null && await _dosPasos.PreferirAsync(user, Metodo))
            Mensaje = Metodo == DosPasos.App
                ? "Ahora se pide la app autenticadora por defecto."
                : "Ahora se pide el código por correo por defecto.";
        return RedirectToPage();
    }

    private async Task<(ApplicationUser?, IActionResult?)> CargarAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return (null, Challenge());

        Estado = await _dosPasos.EstadoAsync(user);
        Email = user.Email;

        // Una sesión abierta solo con contraseña no puede tocar una cuenta que ya tiene método.
        if (Estado.Configurado && !DosPasosObligatorioMiddleware.TieneSegundoPaso(User))
        {
            await _signInManager.SignOutAsync();
            return (null, RedirectToPage("./Login"));
        }

        // Solo se configura un método que falta.
        if (!DosPasos.MetodoValido(Metodo) || (Metodo == DosPasos.App && Estado.App) || (Metodo == DosPasos.Correo && Estado.Correo))
            Metodo = null;

        if (Metodo == DosPasos.App)
        {
            var clave = await _dosPasos.ClaveAppAsync(user);
            QrSvg = DosPasos.QrSvg(user.Email!, clave);
            Clave = DosPasos.ClaveLegible(clave);
        }
        return (user, null);
    }
}
