using GestorIncidentesTI.Services;

namespace GestorIncidentesTI.Tests;

public class PortalYCorreoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Gestor de Incidentes TI")]
    [InlineData("gestor de incidentes ti")]
    public void Remitente_vacio_o_con_el_nombre_anterior_usa_el_nombre_visible(string? configurado) =>
        Assert.Equal("Gestor de incidentes TI", Portal.NombreRemitente(configurado));

    [Fact]
    public void Remitente_personalizado_se_respeta() =>
        Assert.Equal("Mesa de ayuda", Portal.NombreRemitente(" Mesa de ayuda "));

    [Fact]
    public void El_QR_usa_el_nombre_visible_como_emisor_sin_tocar_la_clave()
    {
        var uri = DosPasos.UriOtpauth("usuario@ejemplo.com", "JBSWY3DPEHPK3PXP");

        Assert.StartsWith("otpauth://totp/Gestor%20de%20incidentes%20TI%3Ausuario%40ejemplo.com?", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP&", uri);
        Assert.Contains("issuer=Gestor%20de%20incidentes%20TI&", uri);
    }

    [Fact]
    public void Las_plantillas_de_correo_llevan_el_nombre_visible()
    {
        var aviso = new Aviso("Encabezado", "Título", Array.Empty<Chip>(), new[] { ("Campo", "Valor") },
            Array.Empty<(string, string)>(), null, "");

        Assert.Contains("Gestor de incidentes TI", PlantillaCorreo.Html(aviso));
        Assert.Contains("del Gestor de incidentes TI. Lo reciben", PlantillaCorreo.Html(aviso));
        Assert.StartsWith("Gestor de incidentes TI — Encabezado", PlantillaCorreo.Texto(aviso));
        Assert.Contains("Gestor de incidentes TI", PlantillaCorreo.Telegram(aviso));
        Assert.DoesNotContain("Gestor de Incidentes TI", PlantillaCorreo.Html(aviso));
    }

    [Fact]
    public async Task El_correo_sale_por_Brevo_solo_al_titular_con_el_remitente_descriptivo()
    {
        using var entorno = new Entorno(remitenteNombre: "Gestor de Incidentes TI");
        var correo = entorno.Servicio<CorreoBrevo>();

        var enviado = await correo.EnviarAsync("usuario@ejemplo.com", "Asunto",
            new Aviso("E", "T", Array.Empty<Chip>(), Array.Empty<(string, string)>(), Array.Empty<(string, string)>(), null, ""));

        Assert.True(enviado);
        var json = entorno.UltimoCorreo;
        Assert.Equal("Gestor de incidentes TI", json.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("usuario@ejemplo.com", json.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal(1, json.GetProperty("to").GetArrayLength());
        Assert.False(json.TryGetProperty("bcc", out _));
        Assert.False(json.TryGetProperty("cc", out _));
    }

    [Fact]
    public async Task Sin_clave_de_Brevo_no_envia_ni_falla()
    {
        using var entorno = new Entorno();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var correo = new CorreoBrevo(new HttpClient(entorno.Brevo), config,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CorreoBrevo>.Instance);

        await correo.SendEmailAsync("usuario@ejemplo.com", "Asunto", "<p>Hola</p>");

        Assert.Empty(entorno.Brevo.Enviados);
    }

    [Fact]
    public void Totp_cumple_el_vector_de_la_RFC_6238()
    {
        // Clave "12345678901234567890" en Base32; en T = 59 s el código de 8 dígitos es 94287082.
        const string clave = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        var momento = DateTimeOffset.FromUnixTimeSeconds(59);

        Assert.Equal("287082", Totp.Generar(clave, momento));
        Assert.True(Totp.Verificar(clave, "287082", momento.AddSeconds(30)));
        Assert.False(Totp.Verificar(clave, "287082", momento.AddSeconds(90)));
        Assert.False(Totp.Verificar(clave, "28708", momento));
    }

    [Fact]
    public void Los_errores_de_contrasena_estan_en_espanol()
    {
        var errores = new ErroresIdentidad();
        Assert.Equal("La contraseña debe tener al menos un número.", errores.PasswordRequiresDigit().Description);
        Assert.Equal("La contraseña debe tener al menos 6 caracteres.", errores.PasswordTooShort(6).Description);
    }
}
