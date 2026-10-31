namespace GestorIncidentesTI.Models;

/// <summary>
/// Evento de seguridad de una cuenta (ingresos, fallos, bloqueos, cambios de contraseña).
/// Solo el tipo, la cuenta y la fecha; se depura a los 15 días (ver Services/Auditoria).
/// </summary>
public class EventoSeguridad
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}
