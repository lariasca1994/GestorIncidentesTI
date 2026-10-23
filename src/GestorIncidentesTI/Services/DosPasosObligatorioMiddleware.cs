using GestorIncidentesTI.Data;
using Microsoft.AspNetCore.Identity;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Verificación en dos pasos obligatoria. Una sesión vale solo si se abrió con el segundo
/// paso (claim amr = mfa):
/// - si la cuenta aún no tiene método, la única página disponible es la de elegirlo;
/// - si ya lo tiene (sesión vieja o abierta solo con contraseña), se cierra y se vuelve a ingresar.
/// Las páginas de Identity para apagar la verificación o usar códigos de recuperación
/// llevan a Seguridad: la verificación no se puede desactivar.
/// </summary>
public class DosPasosObligatorioMiddleware
{
    public const string ClaimMfa = "mfa";
    private const string Seguridad = "/Identity/Account/Seguridad";

    private static readonly string[] Libres =
    {
        Seguridad, "/Identity/Account/Logout", "/Identity/Account/Login", "/Identity/Account/LoginWith2fa", "/Error"
    };

    private static readonly string[] Reemplazadas =
    {
        "/Identity/Account/Manage/TwoFactorAuthentication", "/Identity/Account/Manage/EnableAuthenticator",
        "/Identity/Account/Manage/Disable2fa", "/Identity/Account/Manage/ResetAuthenticator",
        "/Identity/Account/Manage/GenerateRecoveryCodes", "/Identity/Account/Manage/ShowRecoveryCodes",
        "/Identity/Account/LoginWithRecoveryCode"
    };

    private readonly RequestDelegate _next;

    public DosPasosObligatorioMiddleware(RequestDelegate next) => _next = next;

    public static bool TieneSegundoPaso(System.Security.Claims.ClaimsPrincipal usuario) =>
        usuario.HasClaim("amr", ClaimMfa);

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> usuarios, SignInManager<ApplicationUser> acceso, DosPasos dosPasos)
    {
        var ruta = context.Request.Path;

        if (Reemplazadas.Any(r => ruta.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Redirect(Seguridad);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true || TieneSegundoPaso(context.User)
            || Libres.Any(r => ruta.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var usuario = await usuarios.GetUserAsync(context.User);
        if (usuario is null || (await dosPasos.EstadoAsync(usuario)).Configurado)
        {
            await acceso.SignOutAsync();
            Responder(context, "/Identity/Account/Login");
            return;
        }

        Responder(context, Seguridad);
    }

    /// <summary>La API responde 401; las páginas redirigen.</summary>
    private static void Responder(HttpContext context, string destino)
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var volver = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"{destino}?returnUrl={Uri.EscapeDataString(volver)}");
    }
}
