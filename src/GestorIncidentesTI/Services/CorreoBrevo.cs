using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Correos de la cuenta (código de verificación, recuperación de contraseña o de acceso) por la
/// API transaccional de Brevo, solo al titular y sin copias. Usa la misma configuración que los
/// avisos: Brevo:ApiKey (secret de la Container App), Brevo:RemitenteEmail y
/// Brevo:RemitenteNombre. Sin clave solo se registra en el log (entorno local). Nunca lanza.
/// </summary>
public class CorreoBrevo : IEmailSender
{
    private static readonly System.Text.Json.JsonSerializerOptions SinNulos =
        new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CorreoBrevo> _logger;

    public CorreoBrevo(HttpClient http, IConfiguration configuration, ILogger<CorreoBrevo> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Correo con la plantilla del sitio (HTML y texto plano).</summary>
    public Task<bool> EnviarAsync(string email, string asunto, Aviso aviso) =>
        EnviarAsync(email, asunto, PlantillaCorreo.Html(aviso), PlantillaCorreo.Texto(aviso));

    /// <summary>IEmailSender de Identity: el HTML lo arma quien llama.</summary>
    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        EnviarAsync(email, subject, htmlMessage, null);

    private async Task<bool> EnviarAsync(string email, string asunto, string html, string? texto)
    {
        var apiKey = _configuration["Brevo:ApiKey"];
        var remitente = _configuration["Brevo:RemitenteEmail"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remitente))
        {
            _logger.LogWarning("Correo de la cuenta no enviado: Brevo sin configurar ({Asunto}).", asunto);
            return false;
        }

        var cuerpo = new
        {
            sender = new { email = remitente, name = Portal.NombreRemitente(_configuration["Brevo:RemitenteNombre"]) },
            to = new[] { new { email } },
            subject = asunto,
            htmlContent = html,
            textContent = texto
        };

        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
            {
                Content = JsonContent.Create(cuerpo, options: SinNulos)
            };
            solicitud.Headers.Add("api-key", apiKey);
            using var respuesta = await _http.SendAsync(solicitud);
            if (respuesta.IsSuccessStatusCode) return true;
            _logger.LogError("Brevo rechazó el correo de la cuenta: {Estado}", (int)respuesta.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo de la cuenta.");
        }
        return false;
    }
}
