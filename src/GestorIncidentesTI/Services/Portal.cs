namespace GestorIncidentesTI.Services;

/// <summary>
/// Nombre visible del portal: título de las páginas, correos (asunto, encabezado, pie y
/// remitente) y emisor del QR de la app autenticadora.
/// </summary>
public static class Portal
{
    public const string Nombre = "Gestor de incidentes TI";

    /// <summary>
    /// Nombre del remitente de los correos. Brevo:RemitenteNombre puede venir vacío o con el
    /// nombre anterior ("Gestor de Incidentes TI"): en ambos casos se usa el nombre visible.
    /// </summary>
    public static string NombreRemitente(string? configurado)
    {
        var nombre = configurado?.Trim();
        return string.IsNullOrEmpty(nombre) || nombre.Equals(Nombre, StringComparison.OrdinalIgnoreCase)
            ? Nombre
            : nombre;
    }
}
