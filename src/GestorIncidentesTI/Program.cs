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
.AddDefaultTokenProviders()
.AddErrorDescriber<ErroresIdentidad>();

// Al cambiar la contraseña o la app autenticadora se renueva el SecurityStamp: las demás
// sesiones se cierran en la siguiente revalidación (a lo sumo 5 minutos).
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));

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

// Correos de la cuenta (códigos, recuperación de contraseña o de acceso) por la API de Brevo,
// con la misma configuración que los avisos; también es el IEmailSender de Identity.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<CorreoBrevo>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>(sp => sp.GetRequiredService<CorreoBrevo>());
builder.Services.AddScoped<DosPasos>();
builder.Services.AddScoped<RecuperacionClave>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
   // Azure SQL sin servidor tarda en reanudarse tras la pausa automática y
   // rechaza las primeras conexiones (error 40613); los reintentos lo absorben.
   options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
       maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddScoped<ISlaService, SlaService>();
builder.Services.AddScoped<Auditoria>();

// Avisos por correo (Brevo) al crear o completar incidentes y al crear
// proyectos. La API key va en el secret Brevo__ApiKey de la Container App.
builder.Services.AddHttpClient<INotificador, NotificadorBrevo>(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = Portal.Nombre, Version = "v1" });
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{Portal.Nombre} v1");
        c.RoutePrefix = "swagger";
    });
}

// 404 con página propia (estática: no consulta la base). Solo para páginas pedidas con GET; la
// API y las respuestas que ya traen contenido quedan como siempre.
app.UseStatusCodePages(async contexto =>
{
    var http = contexto.HttpContext;
    if (http.Response.StatusCode != StatusCodes.Status404NotFound || !HttpMethods.IsGet(http.Request.Method)
        || http.Request.Path.StartsWithSegments("/api")) return;
    http.Response.ContentType = "text/html; charset=utf-8";
    await http.Response.SendFileAsync(app.Environment.WebRootFileProvider.GetFileInfo("no-encontrada.html"));
});

app.UseHttpsRedirection();
// Aplicación instalable (PWA): el manifiesto necesita su tipo y el service worker no se guarda
// en la caché del navegador, para que cada versión llegue al momento.
var tiposArchivo = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
tiposArchivo.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = tiposArchivo,
    OnPrepareResponse = archivo =>
    {
        if (archivo.File.Name == "sw.js")
        {
            archivo.Context.Response.Headers.CacheControl = "no-cache";
            archivo.Context.Response.Headers["Service-Worker-Allowed"] = "/";
        }
    }
});
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