using System.Net.Http.Json;

namespace GestorIncidentesTI.Services;

public interface INotificador
{
    /// <summary>
    /// Avisa por correo de una gestión al usuario que la hizo y a los Admin
    /// de Notificaciones:Admins. Nunca lanza: si el envío falla, la gestión
    /// ya quedó guardada y solo se registra el error.
    /// </summary>
    Task NotificarAsync(string? emailActor, string asunto, Aviso aviso);
}

/// <summary>
/// Envía los avisos con la API transaccional de Brevo (plan gratuito: 300
/// correos al día). Configuración: Brevo:ApiKey (secret de la Container App),
/// Brevo:RemitenteEmail (debe estar verificado en Brevo), Brevo:RemitenteNombre
/// y Notificaciones:Admins (lista de correos). Sin ApiKey solo se registra en
/// el log, así el entorno local no necesita Brevo.
/// </summary>
public class NotificadorBrevo : INotificador
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificadorBrevo> _logger;

    public NotificadorBrevo(HttpClient http, IConfiguration configuration, ILogger<NotificadorBrevo> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotificarAsync(string? emailActor, string asunto, Aviso aviso)
    {
        var destinatarios = _configuration.GetSection("Notificaciones:Admins").GetChildren()
            .Select(s => s.Value)
            .Append(emailActor)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var apiKey = _configuration["Brevo:ApiKey"];
        var remitente = _configuration["Brevo:RemitenteEmail"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remitente) || destinatarios.Count == 0)
        {
            _logger.LogInformation("Correo no enviado (Brevo sin configurar): {Asunto} → {Destinatarios}", asunto, string.Join(", ", destinatarios));
            return;
        }

        var cuerpo = new
        {
            sender = new { email = remitente, name = _configuration["Brevo:RemitenteNombre"] ?? "Gestor de Incidentes TI" },
            to = destinatarios.Select(e => new { email = e }),
            subject = asunto,
            htmlContent = PlantillaCorreo.Html(aviso),
            textContent = PlantillaCorreo.Texto(aviso)
        };

        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
            {
                Content = JsonContent.Create(cuerpo)
            };
            solicitud.Headers.Add("api-key", apiKey);

            using var respuesta = await _http.SendAsync(solicitud);
            if (respuesta.IsSuccessStatusCode)
                _logger.LogInformation("Correo enviado: {Asunto} → {Cantidad} destinatario(s).", asunto, destinatarios.Count);
            else
                _logger.LogError("Brevo rechazó el correo \"{Asunto}\": {Estado} {Detalle}", asunto,
                    (int)respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo \"{Asunto}\".", asunto);
        }
    }
}
