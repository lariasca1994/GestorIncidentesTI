using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GestorIncidentesTI.Data;
using Microsoft.AspNetCore.Identity;
using QRCoder;

namespace GestorIncidentesTI.Services;

public enum ResultadoCodigo { Correcto, Incorrecto, Vencido, Bloqueado }

/// <summary>Métodos activos y el que se pide por defecto ("app" o "correo").</summary>
public sealed record EstadoDosPasos(bool App, bool Correo, string? Preferido)
{
    public bool Configurado => App || Correo;
}

/// <summary>
/// Verificación en dos pasos obligatoria: app autenticadora (TOTP de Identity) o código
/// por correo (Brevo). Todo vive en AspNetUserTokens bajo el proveedor "DosPasos", así que
/// no hace falta cambiar el esquema. Reglas:
/// - un código de la app no se acepta dos veces;
/// - 5 códigos errados (de la app o del correo) bloquean la cuenta 15 minutos (lockout de Identity);
/// - el código del correo vence en 10 minutos, se guarda solo como huella y se envía
///   como máximo 3 veces cada 10 minutos.
/// </summary>
public class DosPasos
{
    public const string App = "app";
    public const string Correo = "correo";
    private const string Proveedor = "DosPasos";
    private const string Emisor = "Gestor de Incidentes TI";
    private static readonly TimeSpan VigenciaCorreo = TimeSpan.FromMinutes(10);
    private const int EnviosPorVentana = 3;

    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DosPasos> _logger;

    public DosPasos(UserManager<ApplicationUser> usuarios, HttpClient http, IConfiguration configuration, ILogger<DosPasos> logger)
    {
        _usuarios = usuarios;
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public static bool MetodoValido(string? metodo) => metodo is App or Correo;

    public async Task<EstadoDosPasos> EstadoAsync(ApplicationUser usuario)
    {
        var app = await LeerAsync(usuario, "App") == "1";
        var correo = await LeerAsync(usuario, "Correo") == "1";
        var preferido = await LeerAsync(usuario, "Preferido");
        if (!MetodoValido(preferido) || (preferido == App && !app) || (preferido == Correo && !correo))
            preferido = app ? App : correo ? Correo : null;
        return new EstadoDosPasos(app, correo, preferido);
    }

    /// <summary>Clave de la app (Base32). Si la app aún no está activa, se genera una nueva.</summary>
    public async Task<string> ClaveAppAsync(ApplicationUser usuario)
    {
        // Una clave que no generó este flujo (o vieja) no se reutiliza mientras la app no esté activa.
        var activa = (await EstadoAsync(usuario)).App;
        var clave = await _usuarios.GetAuthenticatorKeyAsync(usuario);
        if (!activa && (string.IsNullOrEmpty(clave) || await LeerAsync(usuario, "ClaveNueva") != "1"))
        {
            await _usuarios.ResetAuthenticatorKeyAsync(usuario);
            await GuardarAsync(usuario, "ClaveNueva", "1");
            clave = await _usuarios.GetAuthenticatorKeyAsync(usuario);
        }
        return clave!;
    }

    /// <summary>Código QR (SVG) con la dirección otpauth:// que leen las apps autenticadoras.</summary>
    public static string QrSvg(string email, string clave)
    {
        var etiqueta = Uri.EscapeDataString($"{Emisor}:{email}");
        var uri = $"otpauth://totp/{etiqueta}?secret={clave}&issuer={Uri.EscapeDataString(Emisor)}&digits=6&period=30";
        using var datos = new QRCodeGenerator().CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(datos).GetGraphic(5);
    }

    /// <summary>Clave en grupos de 4 para escribirla a mano.</summary>
    public static string ClaveLegible(string clave) =>
        string.Join(' ', Enumerable.Range(0, (clave.Length + 3) / 4).Select(i => clave.Substring(i * 4, Math.Min(4, clave.Length - i * 4))));

    /// <summary>Envía un código nuevo al correo de la cuenta. False si ya se enviaron 3 en 10 minutos.</summary>
    public async Task<bool> EnviarCodigoAsync(ApplicationUser usuario)
    {
        var ahora = DateTimeOffset.UtcNow;
        var envios = ((await LeerAsync(usuario, "Envios")) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s, out var t) ? t : 0)
            .Where(t => ahora.ToUnixTimeSeconds() - t < VigenciaCorreo.TotalSeconds)
            .ToList();
        if (envios.Count >= EnviosPorVentana) return false;

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        envios.Add(ahora.ToUnixTimeSeconds());
        await GuardarAsync(usuario, "Envios", string.Join(',', envios));
        await GuardarAsync(usuario, "CodigoCorreo",
            $"{Huella(usuario, codigo)}|{ahora.Add(VigenciaCorreo).ToUnixTimeSeconds()}|0");

        await EnviarCorreoAsync(usuario.Email!, codigo);
        return true;
    }

    /// <summary>
    /// Comprueba un código del método indicado. Cuenta los errores en el lockout de Identity
    /// (5 errores bloquean 15 minutos) y limpia el contador al acertar.
    /// </summary>
    public async Task<ResultadoCodigo> VerificarAsync(ApplicationUser usuario, string metodo, string? codigo)
    {
        if (await _usuarios.IsLockedOutAsync(usuario)) return ResultadoCodigo.Bloqueado;

        codigo = new string((codigo ?? "").Where(char.IsDigit).ToArray());
        var resultado = metodo == App
            ? await VerificarAppAsync(usuario, codigo)
            : await VerificarCorreoAsync(usuario, codigo);

        if (resultado == ResultadoCodigo.Correcto)
        {
            await _usuarios.ResetAccessFailedCountAsync(usuario);
            return resultado;
        }

        await _usuarios.AccessFailedAsync(usuario);
        return await _usuarios.IsLockedOutAsync(usuario) ? ResultadoCodigo.Bloqueado : resultado;
    }

    /// <summary>Deja activo el método (tras confirmar su primer código) y la verificación encendida.</summary>
    public async Task ActivarAsync(ApplicationUser usuario, string metodo)
    {
        var estado = await EstadoAsync(usuario);
        await GuardarAsync(usuario, metodo == App ? "App" : "Correo", "1");
        if (metodo == App) await _usuarios.RemoveAuthenticationTokenAsync(usuario, Proveedor, "ClaveNueva");
        if (estado.Preferido is null) await GuardarAsync(usuario, "Preferido", metodo);
        await _usuarios.SetTwoFactorEnabledAsync(usuario, true);
    }

    public async Task<bool> PreferirAsync(ApplicationUser usuario, string metodo)
    {
        var estado = await EstadoAsync(usuario);
        if ((metodo == App && !estado.App) || (metodo == Correo && !estado.Correo)) return false;
        await GuardarAsync(usuario, "Preferido", metodo);
        return true;
    }

    public static string Mensaje(ResultadoCodigo resultado) => resultado switch
    {
        ResultadoCodigo.Vencido => "El código venció o ya se usó. Pide uno nuevo.",
        ResultadoCodigo.Bloqueado => "Demasiados códigos incorrectos. Espera 15 minutos e intenta de nuevo.",
        _ => "El código no es correcto."
    };

    private async Task<ResultadoCodigo> VerificarAppAsync(ApplicationUser usuario, string codigo)
    {
        if (codigo.Length != 6) return ResultadoCodigo.Incorrecto;
        var valido = await _usuarios.VerifyTwoFactorTokenAsync(
            usuario, _usuarios.Options.Tokens.AuthenticatorTokenProvider, codigo);
        if (!valido) return ResultadoCodigo.Incorrecto;

        // Un código vale 30 s (más la tolerancia de Identity): el mismo no se acepta dos veces.
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ultimo = (await LeerAsync(usuario, "UltimoCodigo"))?.Split('|');
        if (ultimo is { Length: 2 } && ultimo[0] == Huella(usuario, codigo)
            && long.TryParse(ultimo[1], out var cuando) && ahora - cuando < 120)
            return ResultadoCodigo.Vencido;

        await GuardarAsync(usuario, "UltimoCodigo", $"{Huella(usuario, codigo)}|{ahora}");
        return ResultadoCodigo.Correcto;
    }

    private async Task<ResultadoCodigo> VerificarCorreoAsync(ApplicationUser usuario, string codigo)
    {
        var partes = (await LeerAsync(usuario, "CodigoCorreo"))?.Split('|');
        if (partes is not { Length: 3 } || !long.TryParse(partes[1], out var vence)
            || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > vence)
            return ResultadoCodigo.Vencido;

        var intentos = int.TryParse(partes[2], out var n) ? n : 0;
        if (intentos >= 5) return ResultadoCodigo.Vencido;

        if (codigo.Length == 6 && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(partes[0]), Encoding.ASCII.GetBytes(Huella(usuario, codigo))))
        {
            await _usuarios.RemoveAuthenticationTokenAsync(usuario, Proveedor, "CodigoCorreo");
            return ResultadoCodigo.Correcto;
        }

        await GuardarAsync(usuario, "CodigoCorreo", $"{partes[0]}|{vence}|{intentos + 1}");
        return ResultadoCodigo.Incorrecto;
    }

    /// <summary>Huella del código: el código mismo nunca se guarda.</summary>
    private static string Huella(ApplicationUser usuario, string codigo) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(usuario.Id), Encoding.UTF8.GetBytes(codigo)));

    private Task<string?> LeerAsync(ApplicationUser usuario, string nombre) =>
        _usuarios.GetAuthenticationTokenAsync(usuario, Proveedor, nombre);

    private Task GuardarAsync(ApplicationUser usuario, string nombre, string valor) =>
        _usuarios.SetAuthenticationTokenAsync(usuario, Proveedor, nombre, valor);

    /// <summary>Código por la API transaccional de Brevo, solo al titular (sin copias).</summary>
    private async Task EnviarCorreoAsync(string email, string codigo)
    {
        var apiKey = _configuration["Brevo:ApiKey"];
        var remitente = _configuration["Brevo:RemitenteEmail"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remitente))
        {
            _logger.LogWarning("Código de verificación no enviado: Brevo sin configurar.");
            return;
        }

        var aviso = new Aviso(
            "Verificación en dos pasos",
            $"Tu código de verificación es {codigo}",
            Array.Empty<Chip>(),
            new[] { ("Código", codigo), ("Vence en", "10 minutos"), ("Sirve", "Una sola vez") },
            new[] { ("Si no fuiste tú", "Ignora este correo y cambia tu contraseña: alguien la conoce.") },
            null,
            "",
            "Correo automático del Gestor de Incidentes TI. Nunca te pediremos este código por otro medio.");

        var cuerpo = new
        {
            sender = new { email = remitente, name = _configuration["Brevo:RemitenteNombre"] ?? Emisor },
            to = new[] { new { email } },
            subject = $"{codigo} es tu código del Gestor de Incidentes TI",
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
            if (!respuesta.IsSuccessStatusCode)
                _logger.LogError("Brevo rechazó el código de verificación: {Estado}", (int)respuesta.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el código de verificación.");
        }
    }
}
