using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GestorIncidentesTI.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace GestorIncidentesTI.Services;

/// <summary>Resultado de guardar la contraseña nueva con el enlace del correo.</summary>
public sealed record Restablecimiento(ApplicationUser Usuario, IdentityResult Resultado, IReadOnlyList<string> CodigosRecuperacion);

/// <summary>
/// "Olvidé mi contraseña" con un enlace propio, independiente de las claves de cifrado del
/// contenedor (que se pierden cuando escala a cero):
/// - el enlace lleva un valor aleatorio de 256 bits; en la base queda solo su huella y su vencimiento;
/// - vence en 30 minutos y sirve una sola vez (pedir otro invalida el anterior);
/// - se envían como máximo 3 enlaces cada 30 minutos por cuenta;
/// - al cambiar la contraseña se renueva el SecurityStamp (se cierran las sesiones abiertas), se
///   levanta el bloqueo por intentos, se generan códigos de recuperación nuevos (anulan todos
///   los anteriores) y la verificación en dos pasos sigue igual.
/// Todo vive en AspNetUserTokens bajo el proveedor "Recuperacion".
/// </summary>
public class RecuperacionClave
{
    private const string Proveedor = "Recuperacion";
    private const int EnviosPorVentana = 3;
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);

    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly CorreoBrevo _correo;
    private readonly TimeProvider _reloj;

    public RecuperacionClave(UserManager<ApplicationUser> usuarios, CorreoBrevo correo, TimeProvider reloj)
    {
        _usuarios = usuarios;
        _correo = correo;
        _reloj = reloj;
    }

    private long Ahora => _reloj.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>
    /// Envía el enlace si el correo es de una cuenta y no superó el límite de envíos. Quien llama
    /// responde siempre lo mismo, exista o no la cuenta. <paramref name="enlace"/> arma la dirección
    /// a partir del id de la cuenta y del valor del enlace. Devuelve la cuenta si se envió.
    /// </summary>
    public async Task<ApplicationUser?> SolicitarAsync(string email, Func<string, string, string> enlace)
    {
        var usuario = await _usuarios.FindByEmailAsync(email.Trim());
        if (usuario?.Email is null) return null;

        var envios = ((await LeerAsync(usuario, "Envios")) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s, out var t) ? t : 0)
            .Where(t => Ahora - t < Vigencia.TotalSeconds)
            .ToList();
        if (envios.Count >= EnviosPorVentana) return null;

        var valor = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        envios.Add(Ahora);
        await GuardarAsync(usuario, "Envios", string.Join(',', envios));
        await GuardarAsync(usuario, "Enlace",
            $"{Huella(usuario, valor)}|{(Ahora + (long)Vigencia.TotalSeconds).ToString(CultureInfo.InvariantCulture)}");

        var aviso = new Aviso(
            "Restablecer contraseña",
            "Crea una contraseña nueva",
            Array.Empty<Chip>(),
            new[] { ("Cuenta", usuario.Email), ("Vence en", $"{(int)Vigencia.TotalMinutes} minutos"), ("Sirve", "Una sola vez") },
            new[]
            {
                ("Qué pasa al cambiarla", "Se cierran las sesiones abiertas en otros equipos. Tu verificación en dos pasos sigue igual."),
                ("Si no fuiste tú", "Ignora este correo: tu contraseña no cambia.")
            },
            enlace(usuario.Id, valor),
            "Crear contraseña nueva",
            $"Correo automático del {Portal.Nombre}. Nunca te pediremos tu contraseña por correo.");

        await _correo.EnviarAsync(usuario.Email, $"Restablece tu contraseña del {Portal.Nombre}", aviso);
        return usuario;
    }

    /// <summary>La cuenta del enlace si sigue vigente y sin usar; si no, null.</summary>
    public async Task<ApplicationUser?> ValidarAsync(string? usuarioId, string? valor)
    {
        if (string.IsNullOrEmpty(usuarioId) || string.IsNullOrEmpty(valor) || valor.Length > 100) return null;
        var usuario = await _usuarios.FindByIdAsync(usuarioId);
        if (usuario is null) return null;

        var partes = (await LeerAsync(usuario, "Enlace"))?.Split('|');
        if (partes is not { Length: 2 } || !long.TryParse(partes[1], out var vence) || Ahora > vence) return null;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(partes[0]), Encoding.ASCII.GetBytes(Huella(usuario, valor)))
            ? usuario
            : null;
    }

    /// <summary>
    /// Cambia la contraseña con un enlace válido. Null si el enlace venció o ya se usó; si la
    /// contraseña no cumple las reglas, el resultado trae los errores y el enlace sigue vigente.
    /// Si cambió, trae los códigos de recuperación nuevos para mostrarlos.
    /// </summary>
    public async Task<Restablecimiento?> RestablecerAsync(string? usuarioId, string? valor, string nueva)
    {
        var usuario = await ValidarAsync(usuarioId, valor);
        if (usuario is null) return null;

        // El cambio pasa por Identity (reglas de contraseña y SecurityStamp nuevo).
        var token = await _usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _usuarios.ResetPasswordAsync(usuario, token, nueva);
        if (!resultado.Succeeded) return new Restablecimiento(usuario, resultado, Array.Empty<string>());

        await _usuarios.RemoveAuthenticationTokenAsync(usuario, Proveedor, "Enlace");
        await _usuarios.SetLockoutEndDateAsync(usuario, null);
        await _usuarios.ResetAccessFailedCountAsync(usuario);
        var codigos = (await _usuarios.GenerateNewTwoFactorRecoveryCodesAsync(usuario, DosPasos.CantidadCodigosRecuperacion))?.ToList()
            ?? new List<string>();
        // Cierra toda sesión abierta con la contraseña anterior.
        await _usuarios.UpdateSecurityStampAsync(usuario);
        return new Restablecimiento(usuario, resultado, codigos);
    }

    private static string Huella(ApplicationUser usuario, string valor) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(usuario.Id), Encoding.UTF8.GetBytes(valor)));

    private Task<string?> LeerAsync(ApplicationUser usuario, string nombre) =>
        _usuarios.GetAuthenticationTokenAsync(usuario, Proveedor, nombre);

    private Task GuardarAsync(ApplicationUser usuario, string nombre, string valor) =>
        _usuarios.SetAuthenticationTokenAsync(usuario, Proveedor, nombre, valor);
}
