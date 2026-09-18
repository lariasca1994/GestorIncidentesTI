using Microsoft.AspNetCore.Identity.UI.Services;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Implementación mínima de IEmailSender para desarrollo/portafolio: no envía
/// correos reales, solo deja constancia en el log. Evita depender de un SMTP
/// real mientras el proyecto no lo necesita.
/// </summary>
public class EmailSenderFalso : IEmailSender
{
    private readonly ILogger<EmailSenderFalso> _logger;

    public EmailSenderFalso(ILogger<EmailSenderFalso> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Correo simulado a {Email} — Asunto: {Subject}", email, subject);
        return Task.CompletedTask;
    }
}