using GestorIncidentesTI.Data;
using Microsoft.AspNetCore.Identity;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Crea los roles y el usuario administrador si no existen. Ya no corre al
/// arrancar el contenedor: con la app escalada a cero, cualquier visita
/// anónima (bots, monitores) despertaría Azure SQL. Se invoca bajo demanda
/// desde <see cref="EscalamientoBajoDemandaMiddleware"/>.
/// </summary>
public static class SiembraInicial
{
    public static async Task EjecutarAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var rol in new[] { "Admin", "Usuario" })
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        var adminEmail = configuration["AdminSeed:Email"];
        var adminPassword = configuration["AdminSeed:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)
            && await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var resultado = await userManager.CreateAsync(admin, adminPassword);
            if (resultado.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
