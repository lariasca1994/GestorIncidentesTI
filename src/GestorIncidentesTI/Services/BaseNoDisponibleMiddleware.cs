using Microsoft.Data.SqlClient;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Si Azure SQL no responde (pausada, reanudándose o sin la cuota gratuita del
/// mes), muestra la página de mantenimiento (503) en lugar de un error genérico.
/// Cualquier otro error sigue su curso normal.
/// </summary>
public class BaseNoDisponibleMiddleware
{
    // 40613: base no disponible; 40197/40501: servicio ocupado; -2: tiempo de espera;
    // 53/10060/10054: sin conexión de red.
    private static readonly HashSet<int> ErroresSinConexion = [40613, 40197, 40501, -2, 53, 10060, 10054];

    private readonly RequestDelegate _next;

    public BaseNoDisponibleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IWebHostEnvironment entorno, ILogger<BaseNoDisponibleMiddleware> logger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (EsBaseNoDisponible(ex) && !context.Response.HasStarted)
        {
            logger.LogWarning("Base de datos no disponible: se muestra la página de mantenimiento.");
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "3600";
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(entorno.WebRootFileProvider.GetFileInfo("mantenimiento.html"));
        }
    }

    public static bool EsBaseNoDisponible(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is SqlException sql
                && (sql.Errors.Cast<SqlError>().Any(e => ErroresSinConexion.Contains(e.Number))
                    || sql.Message.Contains("free amount allowance", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}
