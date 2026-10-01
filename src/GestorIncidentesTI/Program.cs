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

builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, GestorIncidentesTI.Services.EmailSenderFalso>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
   // Azure SQL sin servidor tarda en reanudarse tras la pausa automática y
   // rechaza las primeras conexiones (error 40613); los reintentos lo absorben.
   options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
       maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddScoped<ISlaService, SlaService>();

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
app.UseAuthorization();

// Va después de UseAuthentication/UseAuthorization: necesita saber si el
// usuario inició sesión para decidir si evalúa los escalamientos.
app.UseMiddleware<EscalamientoBajoDemandaMiddleware>();

app.MapControllers();
app.MapRazorPages();

app.Run();