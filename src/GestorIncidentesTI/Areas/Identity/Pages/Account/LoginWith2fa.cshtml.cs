using System.Security.Claims;
using GestorIncidentesTI.Data;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account;

/// <summary>
/// Segundo paso del ingreso: el pase de Identity (cookie TwoFactorUserId, 10 minutos) dice
/// quién ya puso la contraseña. Se pide el código del método preferido, con la opción de
/// usar el otro si está activo.
/// </summary>
public class LoginWith2faModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DosPasos _dosPasos;
    private readonly ILogger<LoginWith2faModel> _logger;

    public LoginWith2faModel(SignInManager<ApplicationUser> signInManager, DosPasos dosPasos, ILogger<LoginWith2faModel> logger)
    {
        _signInManager = signInManager;
        _dosPasos = dosPasos;
        _logger = logger;
    }

    [BindProperty]
    public string? Codigo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Metodo { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public EstadoDosPasos Estado { get; private set; } = new(false, false, null);

    public string? Aviso { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await CargarAsync();
        if (user is null) return VolverAlIngreso();

        // Con el correo, el código se envía al abrir la página (máximo 3 cada 10 minutos).
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

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await CargarAsync();
        if (user is null) return VolverAlIngreso();

        var resultado = await _dosPasos.VerificarAsync(user, Metodo!, Codigo);
        var auditoria = HttpContext.RequestServices.GetRequiredService<Auditoria>();
        if (resultado != ResultadoCodigo.Correcto)
        {
            await auditoria.RegistrarAsync(Auditoria.CodigoErrado, user);
            _logger.LogWarning("Código de verificación incorrecto para el usuario {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, DosPasos.Mensaje(resultado));
            return Page();
        }

        await auditoria.RegistrarAsync(Auditoria.Ingreso, user);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        await _signInManager.SignInWithClaimsAsync(user, RememberMe,
            new[] { new Claim("amr", DosPasosObligatorioMiddleware.ClaimMfa) });
        _logger.LogInformation("Usuario {UserId} ingresó con verificación en dos pasos ({Metodo}).", user.Id, Metodo);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }

    private async Task<ApplicationUser?> CargarAsync()
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return null;
        Estado = await _dosPasos.EstadoAsync(user);
        if (!Estado.Configurado) return null;
        if (!DosPasos.MetodoValido(Metodo) || (Metodo == DosPasos.App && !Estado.App) || (Metodo == DosPasos.Correo && !Estado.Correo))
            Metodo = Estado.Preferido;
        return user;
    }

    private IActionResult VolverAlIngreso()
    {
        TempData["ErrorMessage"] = "La verificación venció. Ingresa de nuevo.";
        return RedirectToPage("./Login", new { ReturnUrl });
    }
}
