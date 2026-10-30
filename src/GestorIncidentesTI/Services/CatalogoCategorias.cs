namespace GestorIncidentesTI.Services;

/// <summary>
/// Catálogo fijo de categorías y subcategorías. Vive en el código y no en la
/// base: no ocupa espacio ni necesita siembra, y deja los reportes limpios
/// (sin "Infraestructura" e "infra" como categorías distintas).
/// </summary>
public static class CatalogoCategorias
{
    public static readonly IReadOnlyDictionary<string, string[]> Todas = new Dictionary<string, string[]>
    {
        ["Hardware"] = new[] { "Equipo de cómputo", "Impresora", "Periféricos", "Telefonía" },
        ["Software"] = new[] { "Sistema operativo", "Ofimática", "Aplicación de negocio", "Licenciamiento" },
        ["Red y conectividad"] = new[] { "Internet", "Wi-Fi", "VPN", "Red cableada" },
        ["Accesos y cuentas"] = new[] { "Creación de usuario", "Restablecer contraseña", "Permisos", "Bloqueo de cuenta" },
        ["Correo y colaboración"] = new[] { "Correo", "Calendario", "Videoconferencia" },
        ["Infraestructura"] = new[] { "Servidores", "Almacenamiento", "Copias de seguridad", "Nube" },
        ["Seguridad"] = new[] { "Malware", "Phishing", "Incidente de seguridad" },
        ["Otro"] = new[] { "Otro" }
    };

    public static bool EsValida(string? categoria, string? subcategoria) =>
        categoria is not null && subcategoria is not null
        && Todas.TryGetValue(categoria, out var subcategorias)
        && subcategorias.Contains(subcategoria);
}
