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
   options.UseSqlServer(connectionString));

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
// La siembra va protegida: si la base no está disponible al arrancar (por
// ejemplo, pausada por la cuota gratuita), se registra el error y la app
// arranca igual en vez de caerse.
try
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var rol in new[] { "Admin", "Usuario" })
    {
        if (!await roleManager.RoleExistsAsync(rol))
            await roleManager.CreateAsync(new IdentityRole(rol));
    }

    var adminEmail = builder.Configuration["AdminSeed:Email"];
    var adminPassword = builder.Configuration["AdminSeed:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)
        && await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var resultado = await userManager.CreateAsync(admin, adminPassword);
        if (resultado.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "No se pudo ejecutar la siembra inicial; la base no está disponible. La app arranca igual.");
}

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