using GestorIncidentesTI.Data;
using GestorIncidentesTI.Models;
using GestorIncidentesTI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// La cadena de conexión real va en variables de entorno o en Azure App Service
// Configuration (Connection Strings) — nunca en este archivo ni committeada.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la connection string 'Default'.");

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
    // Verificación en dos pasos: 5 códigos errados bloquean la cuenta 15 minutos.
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Identity está alojado bajo /Identity. Sin esta configuración, las páginas
// protegidas redirigen erróneamente a /Account/Login y devuelven 404.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Pase del segundo paso: tras la contraseña hay 10 minutos para escribir el código.
builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
    IdentityConstants.TwoFactorUserIdScheme, options => options.ExpireTimeSpan = TimeSpan.FromMinutes(10));

// La revalidación periódica de la sesión rehace los claims: se conserva la marca del segundo paso.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = contexto =>
{
    if (contexto.CurrentPrincipal is not null && DosPasosObligatorioMiddleware.TieneSegundoPaso(contexto.CurrentPrincipal)
        && contexto.NewPrincipal?.Identity is System.Security.Claims.ClaimsIdentity identidad
        && !identidad.HasClaim("amr", DosPasosObligatorioMiddleware.ClaimMfa))
        identidad.AddClaim(new System.Security.Claims.Claim("amr", DosPasosObligatorioMiddleware.ClaimMfa));
    return Task.CompletedTask;
});

// Código por correo de la verificación en dos pasos (Brevo, misma clave que los avisos).
builder.Services.AddHttpClient<DosPasos>(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, GestorIncidentesTI.Services.EmailSenderFalso>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
   // Azure SQL sin servidor tarda en reanudarse tras la pausa automática y
   // rechaza las primeras conexiones (error 40613); los reintentos lo absorben.
   options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
       maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddScoped<ISlaService, SlaService>();

// Avisos por correo (Brevo) al crear o completar incidentes y al crear
// proyectos. La API key va en el secret Brevo__ApiKey de la Container App.
builder.Services.AddHttpClient<INotificador, NotificadorBrevo>(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Gestor de Incidentes TI", Version = "v1" });
});

var app = builder.Build();

// Las migraciones se aplican como paso explícito de despliegue. Así la cuenta
// de ejecución de la aplicación no necesita permisos de cambio de esquema.
// La siembra de roles y admin no corre al arrancar: la hace
// EscalamientoBajoDemandaMiddleware cuando alguien entra al aplicativo, para
// que el arranque del contenedor no despierte la base pausada.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Base pausada o sin cuota gratuita: página de mantenimiento en vez de error.
app.UseMiddleware<BaseNoDisponibleMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gestor de Incidentes TI v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
// Verificación en dos pasos obligatoria para toda sesión.
app.UseMiddleware<DosPasosObligatorioMiddleware>();
app.UseAuthorization();

// Va después de UseAuthentication/UseAuthorization: necesita saber si el
// usuario inició sesión para decidir si evalúa los escalamientos.
app.UseMiddleware<EscalamientoBajoDemandaMiddleware>();

app.MapControllers();
app.MapRazorPages();

app.Run();