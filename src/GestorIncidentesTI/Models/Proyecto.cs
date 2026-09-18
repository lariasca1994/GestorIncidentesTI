namespace GestorIncidentesTI.Models;

public class Proyecto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? CreadoPorUserId { get; set; }

    public List<Incidente> Incidentes { get; set; } = new();
}