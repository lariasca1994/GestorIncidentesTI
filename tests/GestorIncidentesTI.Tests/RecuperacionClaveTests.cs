using GestorIncidentesTI.Services;

namespace GestorIncidentesTI.Tests;

public class RecuperacionClaveTests
{
    private static (string Usuario, string Codigo) LeerEnlace(Entorno entorno)
    {
        var html = entorno.UltimoCorreo.GetProperty("htmlContent").GetString()!;
        var inicio = html.IndexOf("https://gestor.ejemplo/reset?", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "El correo no trae el enlace.");
        var fin = html.IndexOf('"', inicio);
        var consulta = System.Net.WebUtility.HtmlDecode(html[inicio..fin]).Split('?')[1];
        var partes = consulta.Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        return (partes["usuario"], partes["codigo"]);
    }

    private static Func<string, string, string> Enlace =>
        (id, valor) => $"https://gestor.ejemplo/reset?usuario={id}&codigo={valor}";

    [Fact]
    public async Task Correo_desconocido_no_envia_nada()
    {
        using var entorno = new Entorno();
        var resultado = await entorno.Servicio<RecuperacionClave>().SolicitarAsync("nadie@ejemplo.com", Enlace);

        Assert.Null(resultado);
        Assert.Empty(entorno.Brevo.Enviados);
    }

    [Fact]
    public async Task Envia_el_enlace_por_Brevo_y_guarda_solo_su_huella()
    {
        using var entorno = new Entorno();
        var usuario = await entorno.CrearUsuarioAsync();

        await entorno.Servicio<RecuperacionClave>().SolicitarAsync("usuario@ejemplo.com", Enlace);

        var correo = entorno.UltimoCorreo;
        Assert.Equal("usuario@ejemplo.com", correo.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Restablece tu contraseña del Gestor de incidentes TI", correo.GetProperty("subject").GetString());
        Assert.Equal("Gestor de incidentes TI", correo.GetProperty("sender").GetProperty("name").GetString());
        var (id, codigo) = LeerEnlace(entorno);
        Assert.Equal(usuario.Id, id);

        var guardado = await entorno.Usuarios.GetAuthenticationTokenAsync(usuario, "Recuperacion", "Enlace");
        Assert.NotNull(guardado);
        Assert.DoesNotContain(codigo, guardado);
    }

    [Fact]
    public async Task Restablecer_cambia_la_clave_cierra_sesiones_y_el_enlace_sirve_una_vez()
    {
        using var entorno = new Entorno();
        var (usuario, claveApp) = await entorno.CrearUsuarioConAppAsync();
        var servicio = entorno.Servicio<RecuperacionClave>();
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        var (id, codigo) = LeerEnlace(entorno);
        var sello = await entorno.Usuarios.GetSecurityStampAsync(usuario);
        await entorno.Usuarios.AccessFailedAsync(usuario);

        var cambio = await servicio.RestablecerAsync(id, codigo, "Clave-Nueva-2");

        Assert.NotNull(cambio);
        Assert.True(cambio!.Resultado.Succeeded);
        Assert.True(await entorno.Usuarios.CheckPasswordAsync(usuario, "Clave-Nueva-2"));
        Assert.False(await entorno.Usuarios.CheckPasswordAsync(usuario, Entorno.Clave));
        Assert.NotEqual(sello, await entorno.Usuarios.GetSecurityStampAsync(usuario));
        Assert.Equal(0, await entorno.Usuarios.GetAccessFailedCountAsync(usuario));

        // La verificación en dos pasos sigue igual.
        var estado = await entorno.Servicio<DosPasos>().EstadoAsync(usuario);
        Assert.True(estado.App);
        Assert.True(await entorno.Usuarios.GetTwoFactorEnabledAsync(usuario));
        Assert.Equal(claveApp, await entorno.Usuarios.GetAuthenticatorKeyAsync(usuario));

        // Un solo uso.
        Assert.Null(await servicio.ValidarAsync(id, codigo));
        Assert.Null(await servicio.RestablecerAsync(id, codigo, "Clave-Otra-3"));
    }

    [Fact]
    public async Task Restablecer_genera_codigos_de_recuperacion_nuevos_y_anula_los_anteriores()
    {
        using var entorno = new Entorno();
        var (usuario, _) = await entorno.CrearUsuarioConAppAsync();
        var anteriores = await entorno.Servicio<DosPasos>().NuevosCodigosRecuperacionAsync(usuario);
        var servicio = entorno.Servicio<RecuperacionClave>();
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        var (id, codigo) = LeerEnlace(entorno);

        var cambio = await servicio.RestablecerAsync(id, codigo, "Clave-Nueva-2");

        Assert.Equal(DosPasos.CantidadCodigosRecuperacion, cambio!.CodigosRecuperacion.Count);
        Assert.Equal(DosPasos.CantidadCodigosRecuperacion, await entorno.Usuarios.CountRecoveryCodesAsync(usuario));
        Assert.Empty(anteriores.Intersect(cambio.CodigosRecuperacion));
        Assert.False((await entorno.Usuarios.RedeemTwoFactorRecoveryCodeAsync(usuario, anteriores[0])).Succeeded);
        Assert.True((await entorno.Usuarios.RedeemTwoFactorRecoveryCodeAsync(usuario, cambio.CodigosRecuperacion[0])).Succeeded);
    }

    [Fact]
    public async Task El_enlace_vence_a_los_30_minutos()
    {
        using var entorno = new Entorno();
        await entorno.CrearUsuarioAsync();
        var servicio = entorno.Servicio<RecuperacionClave>();
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        var (id, codigo) = LeerEnlace(entorno);

        entorno.Reloj.Avanzar(TimeSpan.FromMinutes(29));
        Assert.NotNull(await servicio.ValidarAsync(id, codigo));
        entorno.Reloj.Avanzar(TimeSpan.FromMinutes(2));
        Assert.Null(await servicio.ValidarAsync(id, codigo));
    }

    [Fact]
    public async Task Pedir_otro_enlace_anula_el_anterior_y_hay_limite_de_envios()
    {
        using var entorno = new Entorno();
        await entorno.CrearUsuarioAsync();
        var servicio = entorno.Servicio<RecuperacionClave>();

        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        var primero = LeerEnlace(entorno);
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        Assert.Null(await servicio.ValidarAsync(primero.Usuario, primero.Codigo));

        Assert.Null(await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace));
        Assert.Equal(3, entorno.Brevo.Enviados.Count);

        entorno.Reloj.Avanzar(TimeSpan.FromMinutes(31));
        Assert.NotNull(await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace));
    }

    [Fact]
    public async Task Contrasena_debil_muestra_errores_en_espanol_y_el_enlace_sigue_vigente()
    {
        using var entorno = new Entorno();
        await entorno.CrearUsuarioAsync();
        var servicio = entorno.Servicio<RecuperacionClave>();
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        var (id, codigo) = LeerEnlace(entorno);

        var cambio = await servicio.RestablecerAsync(id, codigo, "sinnumero");

        Assert.False(cambio!.Resultado.Succeeded);
        Assert.Contains(cambio.Resultado.Errors, e => e.Description == "La contraseña debe tener al menos un número.");
        Assert.NotNull(await servicio.ValidarAsync(id, codigo));
    }

    [Fact]
    public async Task Enlace_alterado_no_sirve()
    {
        using var entorno = new Entorno();
        await entorno.CrearUsuarioAsync();
        var servicio = entorno.Servicio<RecuperacionClave>();
        await servicio.SolicitarAsync("usuario@ejemplo.com", Enlace);
        var (id, codigo) = LeerEnlace(entorno);

        Assert.Null(await servicio.ValidarAsync(id, codigo + "x"));
        Assert.Null(await servicio.ValidarAsync("otro-id", codigo));
        Assert.Null(await servicio.ValidarAsync(id, null));
    }
}
