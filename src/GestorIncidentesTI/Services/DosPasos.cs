using System.Globalization;
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
/// "Perdí mi autenticador": un código al correo de la cuenta (aunque el método correo no esté
/// activo; vence en 15 minutos, máximo 3 envíos cada 15 minutos) habilita configurar una app
/// nueva. Acertarlo no abre la sesión: la app anterior sigue valiendo hasta activar la nueva,
/// y la sesión se abre solo entonces.
/// </summary>
public class DosPasos
{
    public const string App = "app";
    public const string Correo = "correo";
    public const string Recuperacion = "recuperacion";
    private const string Proveedor = "DosPasos";
    private const int EnviosPorVentana = 3;

    /// <summary>Cada código por correo: dónde se guarda, dónde se cuentan sus envíos y cuánto vale.</summary>
    private sealed record TipoCodigo(string Token, string Envios, TimeSpan Vigencia);

    private static readonly TipoCodigo CodigoCorreo = new("CodigoCorreo", "Envios", TimeSpan.FromMinutes(10));
    private static readonly TipoCodigo CodigoRecuperacion = new("CodigoRecuperacion", "EnviosRecuperacion", TimeSpan.FromMinutes(15));

    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly IUserStore<ApplicationUser> _almacen;
    private readonly CorreoBrevo _correo;
    private readonly TimeProvider _reloj;

    public DosPasos(UserManager<ApplicationUser> usuarios, IUserStore<ApplicationUser> almacen, CorreoBrevo correo, TimeProvider reloj)
    {
        _usuarios = usuarios;
        _almacen = almacen;
        _correo = correo;
        _reloj = reloj;
    }

    private DateTimeOffset Ahora => _reloj.GetUtcNow();

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
        using var datos = new QRCodeGenerator().CreateQrCode(UriOtpauth(email, clave), QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(datos).GetGraphic(5);
    }

    /// <summary>
    /// Dirección otpauth:// con el nombre visible del portal como emisor. El emisor es solo lo
    /// que muestra la app: no cambia la clave de las apps ya configuradas.
    /// </summary>
    public static string UriOtpauth(string email, string clave)
    {
        var etiqueta = Uri.EscapeDataString($"{Portal.Nombre}:{email}");
        return $"otpauth://totp/{etiqueta}?secret={clave}&issuer={Uri.EscapeDataString(Portal.Nombre)}&digits=6&period=30";
    }

    /// <summary>Clave en grupos de 4 para escribirla a mano.</summary>
    public static string ClaveLegible(string clave) =>
        string.Join(' ', Enumerable.Range(0, (clave.Length + 3) / 4).Select(i => clave.Substring(i * 4, Math.Min(4, clave.Length - i * 4))));

    /// <summary>Envía un código nuevo al correo de la cuenta. False si ya se enviaron 3 en 10 minutos.</summary>
    public Task<bool> EnviarCodigoAsync(ApplicationUser usuario) => EnviarAsync(usuario, CodigoCorreo);

    /// <summary>
    /// "Perdí mi autenticador": código al correo de la cuenta aunque el método correo no esté
    /// activo. False si ya se enviaron 3 en 15 minutos.
    /// </summary>
    public Task<bool> EnviarCodigoRecuperacionAsync(ApplicationUser usuario) => EnviarAsync(usuario, CodigoRecuperacion);

    /// <summary>
    /// Comprueba un código de "app", "correo" o "recuperacion". Cuenta los errores en el lockout
    /// de Identity (5 errores bloquean 15 minutos) y limpia el contador al acertar.
    /// </summary>
    public async Task<ResultadoCodigo> VerificarAsync(ApplicationUser usuario, string metodo, string? codigo)
    {
        if (await _usuarios.IsLockedOutAsync(usuario)) return ResultadoCodigo.Bloqueado;

        codigo = SoloDigitos(codigo);
        var resultado = metodo switch
        {
            App => await VerificarAppAsync(usuario, codigo),
            Recuperacion => await VerificarCorreoAsync(usuario, codigo, CodigoRecuperacion),
            _ => await VerificarCorreoAsync(usuario, codigo, CodigoCorreo)
        };
        return await CerrarIntentoAsync(usuario, resultado);
    }

    /// <summary>Tras acertar el código de recuperación: 15 minutos para activar la app nueva.</summary>
    public Task MarcarRecuperacionAsync(ApplicationUser usuario) =>
        GuardarAsync(usuario, "RecuperacionHasta",
            Ahora.Add(CodigoRecuperacion.Vigencia).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

    public async Task<bool> RecuperacionVigenteAsync(ApplicationUser usuario) =>
        long.TryParse(await LeerAsync(usuario, "RecuperacionHasta"), out var hasta) && Ahora.ToUnixTimeSeconds() <= hasta;

    /// <summary>Clave de la app nueva, aparte de la actual: la anterior sigue valiendo hasta activarla.</summary>
    public async Task<string> ClavePendienteAsync(ApplicationUser usuario)
    {
        var clave = await LeerAsync(usuario, "ClavePendiente");
        if (string.IsNullOrEmpty(clave))
        {
            clave = _usuarios.GenerateNewAuthenticatorKey();
            await GuardarAsync(usuario, "ClavePendiente", clave);
        }
        return clave;
    }

    /// <summary>
    /// Activa la app nueva de "Perdí mi autenticador" con su primer código: solo entonces
    /// reemplaza la clave anterior, deja la app activa y cierra las demás sesiones (SecurityStamp).
    /// </summary>
    public async Task<ResultadoCodigo> ReemplazarAppAsync(ApplicationUser usuario, string? codigo)
    {
        if (await _usuarios.IsLockedOutAsync(usuario)) return ResultadoCodigo.Bloqueado;
        var pendiente = await LeerAsync(usuario, "ClavePendiente");
        if (!await RecuperacionVigenteAsync(usuario) || string.IsNullOrEmpty(pendiente)) return ResultadoCodigo.Vencido;

        codigo = SoloDigitos(codigo);
        if (!Totp.Verificar(pendiente, codigo, Ahora))
            return await CerrarIntentoAsync(usuario, ResultadoCodigo.Incorrecto);

        if (_almacen is not IUserAuthenticatorKeyStore<ApplicationUser> claves)
            throw new InvalidOperationException("El almacén de usuarios no guarda claves de autenticador.");
        await claves.SetAuthenticatorKeyAsync(usuario, pendiente, CancellationToken.None);
        await GuardarAsync(usuario, "UltimoCodigo", $"{Huella(usuario, codigo)}|{Ahora.ToUnixTimeSeconds()}");
        await _usuarios.RemoveAuthenticationTokenAsync(usuario, Proveedor, "ClavePendiente");
        await _usuarios.RemoveAuthenticationTokenAsync(usuario, Proveedor, "RecuperacionHasta");
        await ActivarAsync(usuario, App);
        await _usuarios.UpdateSecurityStampAsync(usuario);
        return await CerrarIntentoAsync(usuario, ResultadoCodigo.Correcto);
    }

    /// <summary>Códigos de recuperación de Identity que le quedan a la cuenta.</summary>
    public Task<int> CodigosRecuperacionAsync(ApplicationUser usuario) => _usuarios.CountRecoveryCodesAsync(usuario);

    public const int CantidadCodigosRecuperacion = 10;

    /// <summary>
    /// Genera códigos de recuperación nuevos y anula todos los anteriores en el mismo momento.
    /// Se llama al cambiar la contraseña (desde Seguridad o con "Olvidé mi contraseña"), al
    /// activar la app nueva de "Perdí mi autenticador" y cuando el usuario los pide. Se muestran
    /// una sola vez; cada uno sirve para un ingreso.
    /// </summary>
    public async Task<IReadOnlyList<string>> NuevosCodigosRecuperacionAsync(ApplicationUser usuario) =>
        (await _usuarios.GenerateNewTwoFactorRecoveryCodesAsync(usuario, CantidadCodigosRecuperacion))?.ToList()
        ?? new List<string>();

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

    private async Task<ResultadoCodigo> CerrarIntentoAsync(ApplicationUser usuario, ResultadoCodigo resultado)
    {
        if (resultado == ResultadoCodigo.Correcto)
        {
            await _usuarios.ResetAccessFailedCountAsync(usuario);
            return resultado;
        }
        await _usuarios.AccessFailedAsync(usuario);
        return await _usuarios.IsLockedOutAsync(usuario) ? ResultadoCodigo.Bloqueado : resultado;
    }

    private static string SoloDigitos(string? codigo) => new((codigo ?? "").Where(char.IsAsciiDigit).ToArray());

    private async Task<bool> EnviarAsync(ApplicationUser usuario, TipoCodigo tipo)
    {
        var ahora = Ahora;
        var envios = ((await LeerAsync(usuario, tipo.Envios)) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s, out var t) ? t : 0)
            .Where(t => ahora.ToUnixTimeSeconds() - t < tipo.Vigencia.TotalSeconds)
            .ToList();
        if (envios.Count >= EnviosPorVentana) return false;

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        envios.Add(ahora.ToUnixTimeSeconds());
        await GuardarAsync(usuario, tipo.Envios, string.Join(',', envios));
        await GuardarAsync(usuario, tipo.Token,
            $"{Huella(usuario, codigo)}|{ahora.Add(tipo.Vigencia).ToUnixTimeSeconds()}|0");

        await _correo.EnviarAsync(usuario.Email!, $"{codigo} es tu código del {Portal.Nombre}", AvisoCodigo(tipo, codigo));
        return true;
    }

    private static Aviso AvisoCodigo(TipoCodigo tipo, string codigo)
    {
        var detalles = new[] { ("Código", codigo), ("Vence en", $"{(int)tipo.Vigencia.TotalMinutes} minutos"), ("Sirve", "Una sola vez") };
        var pie = $"Correo automático del {Portal.Nombre}. Nunca te pediremos este código por otro medio.";
        return tipo == CodigoRecuperacion
            ? new Aviso("Recuperar el acceso", $"Tu código para recuperar el acceso es {codigo}", Array.Empty<Chip>(), detalles,
                new[]
                {
                    ("Qué sigue", "Escríbelo en la pantalla \"Perdí mi autenticador\" y configura tu nueva app autenticadora. La app anterior deja de funcionar cuando actives la nueva."),
                    ("Si no fuiste tú", "Ignora este correo y cambia tu contraseña: alguien la conoce.")
                }, null, "", pie)
            : new Aviso("Verificación en dos pasos", $"Tu código de verificación es {codigo}", Array.Empty<Chip>(), detalles,
                new[] { ("Si no fuiste tú", "Ignora este correo y cambia tu contraseña: alguien la conoce.") }, null, "", pie);
    }

    private async Task<ResultadoCodigo> VerificarAppAsync(ApplicationUser usuario, string codigo)
    {
        if (codigo.Length != 6) return ResultadoCodigo.Incorrecto;
        var valido = await _usuarios.VerifyTwoFactorTokenAsync(
            usuario, _usuarios.Options.Tokens.AuthenticatorTokenProvider, codigo);
        if (!valido) return ResultadoCodigo.Incorrecto;

        // Un código vale 30 s (más la tolerancia de Identity): el mismo no se acepta dos veces.
        var ahora = Ahora.ToUnixTimeSeconds();
        var ultimo = (await LeerAsync(usuario, "UltimoCodigo"))?.Split('|');
        if (ultimo is { Length: 2 } && ultimo[0] == Huella(usuario, codigo)
            && long.TryParse(ultimo[1], out var cuando) && ahora - cuando < 120)
            return ResultadoCodigo.Vencido;

        await GuardarAsync(usuario, "UltimoCodigo", $"{Huella(usuario, codigo)}|{ahora}");
        return ResultadoCodigo.Correcto;
    }

    private async Task<ResultadoCodigo> VerificarCorreoAsync(ApplicationUser usuario, string codigo, TipoCodigo tipo)
    {
        var partes = (await LeerAsync(usuario, tipo.Token))?.Split('|');
        if (partes is not { Length: 3 } || !long.TryParse(partes[1], out var vence)
            || Ahora.ToUnixTimeSeconds() > vence)
            return ResultadoCodigo.Vencido;

        var intentos = int.TryParse(partes[2], out var n) ? n : 0;
        if (intentos >= 5) return ResultadoCodigo.Vencido;

        if (codigo.Length == 6 && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(partes[0]), Encoding.ASCII.GetBytes(Huella(usuario, codigo))))
        {
            await _usuarios.RemoveAuthenticationTokenAsync(usuario, Proveedor, tipo.Token);
            return ResultadoCodigo.Correcto;
        }

        await GuardarAsync(usuario, tipo.Token, $"{partes[0]}|{vence}|{intentos + 1}");
        return ResultadoCodigo.Incorrecto;
    }

    /// <summary>Huella del código: el código mismo nunca se guarda.</summary>
    private static string Huella(ApplicationUser usuario, string codigo) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(usuario.Id), Encoding.UTF8.GetBytes(codigo)));

    private Task<string?> LeerAsync(ApplicationUser usuario, string nombre) =>
        _usuarios.GetAuthenticationTokenAsync(usuario, Proveedor, nombre);

    private Task GuardarAsync(ApplicationUser usuario, string nombre, string valor) =>
        _usuarios.SetAuthenticationTokenAsync(usuario, Proveedor, nombre, valor);
}
