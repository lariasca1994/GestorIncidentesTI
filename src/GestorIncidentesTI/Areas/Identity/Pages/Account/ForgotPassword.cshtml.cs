using System.ComponentModel.DataAnnotations;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidentesTI.Areas.Identity.Pages.Account;

/// <summary>
/// Paso 1 de "Olvidé mi contraseña": solo el correo. La respuesta es siempre la misma, exista
/// o no la cuenta; si existe, le llega un enlace de un solo uso que vence (RecuperacionClave).
/// </summary>
public class ForgotPasswordModel : PageModel
{
    private readonly RecuperacionClave _recuperacion;

    public ForgotPasswordModel(RecuperacionClave recuperacion) => _recuperacion = recuperacion;

    [BindProperty]
    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [Display(Name = "Correo")]
    public string Email { get; set; } = "";

    public bool Enviado { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var usuario = await _recuperacion.SolicitarAsync(Email, (id, valor) => Url.Page(
            "/Account/ResetPassword", pageHandler: null,
            values: new { area = "Identity", usuario = id, codigo = valor },
            protocol: Request.Scheme)!);
        if (usuario is not null)
            await HttpContext.RequestServices.GetRequiredService<Auditoria>().RegistrarAsync(Auditoria.Recuperacion, usuario);

        Enviado = true;
        return Page();
    }
}
