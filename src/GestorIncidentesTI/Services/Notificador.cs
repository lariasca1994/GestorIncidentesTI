using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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
/// Notificaciones:Admins (lista de correos) y CORREOS_COPIA (copia oculta de
/// todos los avisos, separados por coma). Sin ApiKey solo se registra en
/// el log, así el entorno local no necesita Brevo.
/// </summary>
public class NotificadorBrevo : INotificador
{
    private static readonly JsonSerializerOptions SinNulos = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

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
        var actor = string.IsNullOrWhiteSpace(emailActor) ? null : emailActor.Trim();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Para: quien hizo la gestión. CC: los administradores. CCO: CORREOS_COPIA
        // (separados por coma). Nadie aparece dos veces.
        var para = Lista(new[] { actor }, vistos);
        var conCopia = Lista(_configuration.GetSection("Notificaciones:Admins").GetChildren().Select(s => s.Value), vistos);
        var copiaOculta = Lista((_configuration["CORREOS_COPIA"] ?? "").Split(','), vistos);

        // Sin actor (no debería pasar), el aviso va a los administradores.
        if (para.Count == 0)
        {
            para = conCopia;
            conCopia = new();
        }

        var apiKey = _configuration["Brevo:ApiKey"];
        var remitente = _configuration["Brevo:RemitenteEmail"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remitente) || para.Count == 0)
        {
            _logger.LogInformation("Correo no enviado (Brevo sin configurar): {Asunto} → {Destinatarios}", asunto, string.Join(", ", para.Concat(conCopia)));
            return;
        }

        // El correo sale de la cuenta verificada en Brevo, pero se presenta con
        // el nombre de quien hizo la gestión y las respuestas le llegan a él.
        var nombreBase = _configuration["Brevo:RemitenteNombre"] ?? "Gestor de Incidentes TI";
        var nombreRemitente = actor is null || actor.Equals(remitente, StringComparison.OrdinalIgnoreCase)
            ? nombreBase
            : $"{actor} vía {nombreBase}";

        var cuerpo = new
        {
            sender = new { email = remitente, name = nombreRemitente },
            replyTo = actor is null ? null : new { email = actor },
            to = para.Select(e => new { email = e }),
            cc = conCopia.Count > 0 ? conCopia.Select(e => new { email = e }) : null,
            bcc = copiaOculta.Count > 0 ? copiaOculta.Select(e => new { email = e }) : null,
            subject = asunto,
            htmlContent = PlantillaCorreo.Html(aviso),
            textContent = PlantillaCorreo.Texto(aviso)
        };

        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
            {
                Content = JsonContent.Create(cuerpo, options: SinNulos)
            };
            solicitud.Headers.Add("api-key", apiKey);

            using var respuesta = await _http.SendAsync(solicitud);
            if (respuesta.IsSuccessStatusCode)
                _logger.LogInformation("Correo enviado: {Asunto} → {Cantidad} destinatario(s).", asunto, para.Count + conCopia.Count + copiaOculta.Count);
            else
                _logger.LogError("Brevo rechazó el correo \"{Asunto}\": {Estado} {Detalle}", asunto,
                    (int)respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo \"{Asunto}\".", asunto);
        }
    }

    /// <summary>Correos válidos, sin vacíos ni repetidos (también entre listas).</summary>
    private static List<string> Lista(IEnumerable<string?> correos, HashSet<string> vistos) =>
        correos.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .Where(vistos.Add)
            .ToList();
}
