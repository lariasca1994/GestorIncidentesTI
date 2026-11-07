using GestorIncidentesTI.Services;

namespace GestorIncidentesTI.Tests;

public class PerdiAutenticadorTests
{
    /// <summary>El código va al inicio del asunto: "123456 es tu código del …".</summary>
    private static string CodigoDelCorreo(Entorno entorno) =>
        entorno.UltimoCorreo.GetProperty("subject").GetString()![..6];

    private static async Task<string> RecuperarAsync(Entorno entorno, GestorIncidentesTI.Data.ApplicationUser usuario)
    {
        var dosPasos = entorno.Servicio<DosPasos>();
        Assert.True(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));
        Assert.Equal(ResultadoCodigo.Correcto, await dosPasos.VerificarAsync(usuario, DosPasos.Recuperacion, CodigoDelCorreo(entorno)));
        await dosPasos.MarcarRecuperacionAsync(usuario);
        return await dosPasos.ClavePendienteAsync(usuario);
    }

    [Fact]
    public async Task El_codigo_llega_al_correo_aunque_el_metodo_correo_no_este_activo()
    {
        using var entorno = new Entorno();
        var (usuario, _) = await entorno.CrearUsuarioConAppAsync();
        var dosPasos = entorno.Servicio<DosPasos>();
        Assert.False((await dosPasos.EstadoAsync(usuario)).Correo);

        Assert.True(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));

        var correo = entorno.UltimoCorreo;
        Assert.Equal("usuario@ejemplo.com", correo.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.EndsWith("es tu código del Gestor de incidentes TI", correo.GetProperty("subject").GetString());
        Assert.Contains("15 minutos", correo.GetProperty("htmlContent").GetString());
        Assert.False((await dosPasos.EstadoAsync(usuario)).Correo);
    }

    [Fact]
    public async Task El_codigo_vence_a_los_15_minutos_y_hay_limite_de_envios()
    {
        using var entorno = new Entorno();
        var (usuario, _) = await entorno.CrearUsuarioConAppAsync();
        var dosPasos = entorno.Servicio<DosPasos>();

        Assert.True(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));
        Assert.True(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));
        Assert.True(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));
        Assert.False(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));

        var ultimo = CodigoDelCorreo(entorno);
        entorno.Reloj.Avanzar(TimeSpan.FromMinutes(16));
        Assert.Equal(ResultadoCodigo.Vencido, await dosPasos.VerificarAsync(usuario, DosPasos.Recuperacion, ultimo));
        Assert.True(await dosPasos.EnviarCodigoRecuperacionAsync(usuario));
    }

    [Fact]
    public async Task Acertar_el_codigo_del_correo_no_reemplaza_la_app_anterior()
    {
        using var entorno = new Entorno();
        var (usuario, claveAnterior) = await entorno.CrearUsuarioConAppAsync();

        var pendiente = await RecuperarAsync(entorno, usuario);

        Assert.NotEqual(claveAnterior, pendiente);
        Assert.Equal(claveAnterior, await entorno.Usuarios.GetAuthenticatorKeyAsync(usuario));
        Assert.True((await entorno.Servicio<DosPasos>().EstadoAsync(usuario)).App);
    }

    [Fact]
    public async Task Activar_la_app_nueva_reemplaza_la_anterior_cierra_sesiones_y_renueva_codigos()
    {
        using var entorno = new Entorno();
        var (usuario, claveAnterior) = await entorno.CrearUsuarioConAppAsync();
        var dosPasos = entorno.Servicio<DosPasos>();
        var codigosAnteriores = await dosPasos.NuevosCodigosRecuperacionAsync(usuario);
        var pendiente = await RecuperarAsync(entorno, usuario);
        var sello = await entorno.Usuarios.GetSecurityStampAsync(usuario);

        // Código errado: nada cambia.
        var correcto = Totp.Generar(pendiente, entorno.Reloj.Ahora);
        Assert.Equal(ResultadoCodigo.Incorrecto, await dosPasos.ReemplazarAppAsync(usuario, correcto == "000000" ? "111111" : "000000"));
        Assert.Equal(claveAnterior, await entorno.Usuarios.GetAuthenticatorKeyAsync(usuario));

        var resultado = await dosPasos.ReemplazarAppAsync(usuario, correcto);
        var codigosNuevos = await dosPasos.NuevosCodigosRecuperacionAsync(usuario);

        Assert.Equal(ResultadoCodigo.Correcto, resultado);
        Assert.Equal(pendiente, await entorno.Usuarios.GetAuthenticatorKeyAsync(usuario));
        Assert.NotEqual(sello, await entorno.Usuarios.GetSecurityStampAsync(usuario));
        Assert.True((await dosPasos.EstadoAsync(usuario)).App);
        Assert.False(await dosPasos.RecuperacionVigenteAsync(usuario));
        Assert.False((await entorno.Usuarios.RedeemTwoFactorRecoveryCodeAsync(usuario, codigosAnteriores[0])).Succeeded);
        Assert.True((await entorno.Usuarios.RedeemTwoFactorRecoveryCodeAsync(usuario, codigosNuevos[0])).Succeeded);
    }

    [Fact]
    public async Task Sin_verificar_el_correo_no_se_puede_activar_otra_app()
    {
        using var entorno = new Entorno();
        var (usuario, claveAnterior) = await entorno.CrearUsuarioConAppAsync();
        var dosPasos = entorno.Servicio<DosPasos>();
        var pendiente = await dosPasos.ClavePendienteAsync(usuario);

        var resultado = await dosPasos.ReemplazarAppAsync(usuario, Totp.Generar(pendiente, entorno.Reloj.Ahora));

        Assert.Equal(ResultadoCodigo.Vencido, resultado);
        Assert.Equal(claveAnterior, await entorno.Usuarios.GetAuthenticatorKeyAsync(usuario));
    }

    [Fact]
    public async Task La_recuperacion_vence_a_los_15_minutos()
    {
        using var entorno = new Entorno();
        var (usuario, claveAnterior) = await entorno.CrearUsuarioConAppAsync();
        var pendiente = await RecuperarAsync(entorno, usuario);

        entorno.Reloj.Avanzar(TimeSpan.FromMinutes(16));
        var resultado = await entorno.Servicio<DosPasos>().ReemplazarAppAsync(usuario, Totp.Generar(pendiente, entorno.Reloj.Ahora));

        Assert.Equal(ResultadoCodigo.Vencido, resultado);
        Assert.Equal(claveAnterior, await entorno.Usuarios.GetAuthenticatorKeyAsync(usuario));
    }

    [Fact]
    public async Task Cinco_codigos_errados_bloquean_la_cuenta()
    {
        using var entorno = new Entorno();
        var (usuario, _) = await entorno.CrearUsuarioConAppAsync();
        var dosPasos = entorno.Servicio<DosPasos>();
        await dosPasos.EnviarCodigoRecuperacionAsync(usuario);
        var correcto = CodigoDelCorreo(entorno);
        var errado = correcto == "000000" ? "111111" : "000000";

        for (var i = 0; i < 4; i++)
            Assert.Equal(ResultadoCodigo.Incorrecto, await dosPasos.VerificarAsync(usuario, DosPasos.Recuperacion, errado));
        Assert.Equal(ResultadoCodigo.Bloqueado, await dosPasos.VerificarAsync(usuario, DosPasos.Recuperacion, errado));
        Assert.Equal(ResultadoCodigo.Bloqueado, await dosPasos.VerificarAsync(usuario, DosPasos.Recuperacion, correcto));
        Assert.True(await entorno.Usuarios.IsLockedOutAsync(usuario));
    }

    [Fact]
    public async Task Generar_codigos_nuevos_anula_todos_los_anteriores()
    {
        using var entorno = new Entorno();
        var (usuario, _) = await entorno.CrearUsuarioConAppAsync();
        var dosPasos = entorno.Servicio<DosPasos>();

        var primeros = await dosPasos.NuevosCodigosRecuperacionAsync(usuario);
        var segundos = await dosPasos.NuevosCodigosRecuperacionAsync(usuario);

        Assert.Equal(DosPasos.CantidadCodigosRecuperacion, segundos.Count);
        Assert.Equal(DosPasos.CantidadCodigosRecuperacion, await dosPasos.CodigosRecuperacionAsync(usuario));
        foreach (var codigo in primeros)
            Assert.False((await entorno.Usuarios.RedeemTwoFactorRecoveryCodeAsync(usuario, codigo)).Succeeded);
    }

    [Fact]
    public async Task El_codigo_de_verificacion_por_correo_lleva_el_nombre_visible()
    {
        using var entorno = new Entorno();
        var usuario = await entorno.CrearUsuarioAsync();

        Assert.True(await entorno.Servicio<DosPasos>().EnviarCodigoAsync(usuario));

        Assert.EndsWith("es tu código del Gestor de incidentes TI", entorno.UltimoCorreo.GetProperty("subject").GetString());
        Assert.Equal("Gestor de incidentes TI", entorno.UltimoCorreo.GetProperty("sender").GetProperty("name").GetString());
    }
}
