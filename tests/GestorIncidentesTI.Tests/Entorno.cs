using System.Net;
using System.Text.Json;
using GestorIncidentesTI.Data;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestorIncidentesTI.Tests;

/// <summary>Reloj que se mueve a mano.</summary>
public sealed class RelojPrueba : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Ahora;
    public void Avanzar(TimeSpan tiempo) => Ahora = Ahora.Add(tiempo);
}

/// <summary>Captura lo que se enviaría a Brevo, sin salir a internet.</summary>
public sealed class BrevoFalso : HttpMessageHandler
{
    public List<JsonElement> Enviados { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var json = await request.Content!.ReadAsStringAsync(cancellationToken);
        Enviados.Add(JsonDocument.Parse(json).RootElement.Clone());
        return new HttpResponseMessage(HttpStatusCode.Created);
    }
}

/// <summary>Identity sobre una base en memoria, con Brevo y el reloj falsos.</summary>
public sealed class Entorno : IDisposable
{
    public const string Clave = "Clave-Inicial-1";

    private readonly ServiceProvider _raiz;
    private readonly IServiceScope _scope;

    public RelojPrueba Reloj { get; } = new();
    public BrevoFalso Brevo { get; } = new();
    public IConfiguration Configuracion { get; }

    public Entorno(string? remitenteNombre = "Gestor de Incidentes TI")
    {
        var valores = new Dictionary<string, string?>
        {
            ["Brevo:ApiKey"] = "clave-de-prueba",
            ["Brevo:RemitenteEmail"] = "remitente@ejemplo.com",
            ["Brevo:RemitenteNombre"] = remitenteNombre
        };
        Configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDataProtection();
        servicios.AddSingleton(Configuracion);
        servicios.AddSingleton<TimeProvider>(Reloj);
        servicios.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        servicios.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<ErroresIdentidad>();
        servicios.AddScoped(_ => new CorreoBrevo(new HttpClient(Brevo), Configuracion, NullLogger<CorreoBrevo>.Instance));
        servicios.AddScoped<DosPasos>();
        servicios.AddScoped<RecuperacionClave>();

        _raiz = servicios.BuildServiceProvider();
        _scope = _raiz.CreateScope();
    }

    public T Servicio<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public UserManager<ApplicationUser> Usuarios => Servicio<UserManager<ApplicationUser>>();

    public async Task<ApplicationUser> CrearUsuarioAsync(string email = "usuario@ejemplo.com")
    {
        var usuario = new ApplicationUser { UserName = email, Email = email };
        var resultado = await Usuarios.CreateAsync(usuario, Clave);
        Assert.True(resultado.Succeeded, string.Join(", ", resultado.Errors.Select(e => e.Description)));
        await Usuarios.SetLockoutEnabledAsync(usuario, true);
        return usuario;
    }

    /// <summary>Cuenta con la app autenticadora activa, como la deja la página Seguridad.</summary>
    public async Task<(ApplicationUser Usuario, string ClaveApp)> CrearUsuarioConAppAsync()
    {
        var usuario = await CrearUsuarioAsync();
        var dosPasos = Servicio<DosPasos>();
        var clave = await dosPasos.ClaveAppAsync(usuario);
        await dosPasos.ActivarAsync(usuario, DosPasos.App);
        return (usuario, clave);
    }

    public JsonElement UltimoCorreo => Brevo.Enviados[^1];

    public void Dispose()
    {
        _scope.Dispose();
        _raiz.Dispose();
    }
}
