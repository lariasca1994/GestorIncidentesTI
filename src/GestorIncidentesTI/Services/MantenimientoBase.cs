using GestorIncidentesTI.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Mantenimiento que antes era un paso manual del despliegue. Corre una vez por
/// arranque del contenedor, en la primera visita que ya despierta la base (la
/// prueba de QA o un usuario), así nunca despierta Azure SQL por su cuenta:
/// 1. Aplica las migraciones pendientes: un push a main basta para desplegar.
/// 2. Depura lo que no sirve para que la base no crezca: los incidentes de la
///    prueba automática de QA se conservan 15 días y luego se borran con su
///    historial.
/// </summary>
public static class MantenimientoBase
{
    public const int DiasRetencionQa = 15;

    // Firma exacta de los incidentes que crea la prueba E2E de qa-evidencia.
    private const string PrefijoTituloQa = "Incidente QA ";
    private const string DescripcionQa = "Incidente creado por la prueba E2E de qa-evidencia";

    public static async Task MigrarAsync(ApplicationDbContext db, ILogger logger)
    {
        await ConciliarHistorialAsync(db);

        var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendientes.Count == 0) return;

        await db.Database.MigrateAsync();
        logger.LogInformation("Migraciones aplicadas: {Migraciones}.", string.Join(", ", pendientes));
    }

    public static async Task<int> DepurarAsync(ApplicationDbContext db, ILogger logger)
    {
        var limite = DateTime.UtcNow.AddDays(-DiasRetencionQa);

        // El historial se borra en cascada (FK con ON DELETE CASCADE).
        var borrados = await db.Incidentes
            .Where(i => i.Titulo.StartsWith(PrefijoTituloQa)
                        && i.Descripcion == DescripcionQa
                        && i.FechaCreacion < limite)
            .ExecuteDeleteAsync();

        if (borrados > 0)
            logger.LogInformation("Depuración: {Cantidad} incidente(s) de QA con más de {Dias} días borrados.", borrados, DiasRetencionQa);
        return borrados;
    }

    /// <summary>
    /// Las migraciones se aplicaban a mano con un script. Si alguna quedó
    /// aplicada sin su fila en __EFMigrationsHistory, EF intentaría repetirla y
    /// fallaría; aquí se registra la que ya existe en el esquema. No cambia nada
    /// si el historial está completo.
    /// </summary>
    private static Task ConciliarHistorialAsync(ApplicationDbContext db) =>
        db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[Incidentes]') IS NULL RETURN;

            IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));

            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260915024615_InicialSchema')
                INSERT INTO [__EFMigrationsHistory] VALUES (N'20260915024615_InicialSchema', N'8.0.8');

            IF OBJECT_ID(N'[Proyectos]') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916220858_AgregarIdentityYProyectos')
                INSERT INTO [__EFMigrationsHistory] VALUES (N'20260916220858_AgregarIdentityYProyectos', N'8.0.8');

            IF COL_LENGTH(N'HistorialEstados', N'ModificadoPorUserId') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260917000000_AgregarAuditoriaDeUsuarios')
                INSERT INTO [__EFMigrationsHistory] VALUES (N'20260917000000_AgregarAuditoriaDeUsuarios', N'8.0.8');
            """);
}
