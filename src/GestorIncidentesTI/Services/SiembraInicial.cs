using GestorIncidentesTI.Data;
using Microsoft.AspNetCore.Identity;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Deja la base con los roles y las cuentas Admin fijas. Ya no corre al
/// arrancar el contenedor: con la app escalada a cero, cualquier visita
/// anónima (bots, monitores) despertaría Azure SQL. Se invoca bajo demanda
/// desde <see cref="EscalamientoBajoDemandaMiddleware"/>.
///
/// Las cuentas salen de la configuración "UsuariosAdmin" (secrets de la
/// Container App: UsuariosAdmin__0__Email, UsuariosAdmin__0__Password, ...).
/// La configuración es la fuente de verdad: si una cuenta falta se crea, y si
/// existe se le restablece la contraseña, se desbloquea y se asegura el rol.
/// Son las únicas con rol Admin: a cualquier otra se le quita y pasa a Usuario.
/// </summary>
public static class SiembraInicial
{
    private sealed record CuentaAdmin(string Email, string Password);

    public static async Task EjecutarAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var rol in new[] { "Admin", "Usuario" })
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        var cuentas = LeerCuentas(configuration);
        if (cuentas.Count == 0)
        {
            logger.LogWarning("No hay cuentas en UsuariosAdmin: no se toca el rol Admin de nadie.");
            return;
        }

        foreach (var cuenta in cuentas)
            await AsegurarAdminAsync(userManager, cuenta, logger);

        var emailsAdmin = cuentas.Select(c => c.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var otro in await userManager.GetUsersInRoleAsync("Admin"))
        {
            if (otro.Email is not null && emailsAdmin.Contains(otro.Email)) continue;

            await userManager.RemoveFromRoleAsync(otro, "Admin");
            if (!await userManager.IsInRoleAsync(otro, "Usuario"))
                await userManager.AddToRoleAsync(otro, "Usuario");
            logger.LogWarning("Se quitó el rol Admin a {Email}: no está en UsuariosAdmin.", otro.Email);
        }
    }

    private static List<CuentaAdmin> LeerCuentas(IConfiguration configuration)
    {
        var cuentas = configuration.GetSection("UsuariosAdmin").GetChildren()
            .Select(s => new CuentaAdmin(s["Email"] ?? "", s["Password"] ?? ""))
            .Where(c => !string.IsNullOrWhiteSpace(c.Email) && !string.IsNullOrWhiteSpace(c.Password))
            .ToList();

        // Compatibilidad con la configuración anterior de un solo admin.
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        if (cuentas.Count == 0 && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            cuentas.Add(new CuentaAdmin(email, password));

        return cuentas;
    }

    private static async Task AsegurarAdminAsync(UserManager<ApplicationUser> userManager, CuentaAdmin cuenta, ILogger logger)
    {
        var usuario = await userManager.FindByEmailAsync(cuenta.Email);

        if (usuario is null)
        {
            usuario = new ApplicationUser { UserName = cuenta.Email, Email = cuenta.Email, EmailConfirmed = true };
            var creado = await userManager.CreateAsync(usuario, cuenta.Password);
            if (!creado.Succeeded)
            {
                logger.LogError("No se pudo crear {Email}: {Errores}", cuenta.Email,
                    string.Join("; ", creado.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Cuenta Admin creada: {Email}.", cuenta.Email);
        }
        else
        {
            if (!await userManager.CheckPasswordAsync(usuario, cuenta.Password))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
                var cambio = await userManager.ResetPasswordAsync(usuario, token, cuenta.Password);
                if (!cambio.Succeeded)
                    logger.LogError("No se pudo restablecer la contraseña de {Email}: {Errores}", cuenta.Email,
                        string.Join("; ", cambio.Errors.Select(e => e.Description)));
            }

            // El Admin no pertenece a un solo proyecto.
            if (!usuario.EmailConfirmed || usuario.ProyectoId is not null || usuario.LockoutEnd is not null)
            {
                usuario.EmailConfirmed = true;
                usuario.ProyectoId = null;
                usuario.LockoutEnd = null;
                usuario.AccessFailedCount = 0;
                await userManager.UpdateAsync(usuario);
            }
        }

        if (!await userManager.IsInRoleAsync(usuario, "Admin"))
            await userManager.AddToRoleAsync(usuario, "Admin");
        if (await userManager.IsInRoleAsync(usuario, "Usuario"))
            await userManager.RemoveFromRoleAsync(usuario, "Usuario");
    }
}
