namespace GestorIncidentesTI.Services;

/// <summary>
/// Evalúa los escalamientos de SLA bajo demanda: se ejecuta cuando un usuario
/// autenticado navega por la app, como máximo una vez cada 5 minutos.
/// Reemplaza al BackgroundService que consultaba la base cada 5 minutos las
/// 24 horas, lo que impedía que Azure SQL sin servidor se pausara y agotaba
/// la cuota gratuita. Sin visitas no hay consultas, y la base puede dormir.
/// </summary>
public class EscalamientoBajoDemandaMiddleware
{
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static long _ultimaEjecucionTicks;

    private readonly RequestDelegate _next;

    public EscalamientoBajoDemandaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISlaService slaService,
        ILogger<EscalamientoBajoDemandaMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && HttpMethods.IsGet(context.Request.Method)
            && TocaEvaluar()
            && await Candado.WaitAsync(0))
        {
            try
            {
                if (TocaEvaluar())
                {
                    var escalados = await slaService.EvaluarEscalamientosAsync();

                    if (escalados > 0)
                        logger.LogInformation("Escalamiento bajo demanda: {Cantidad} incidente(s) escalado(s).", escalados);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluando escalamientos bajo demanda.");
            }
            finally
            {
                // Se marca también si falló, para no reintentar en cada petición
                // mientras la base no esté disponible.
                Interlocked.Exchange(ref _ultimaEjecucionTicks, DateTime.UtcNow.Ticks);
                Candado.Release();
            }
        }

        await _next(context);
    }

    private static bool TocaEvaluar()
    {
        var ultima = new DateTime(Interlocked.Read(ref _ultimaEjecucionTicks), DateTimeKind.Utc);
        return DateTime.UtcNow - ultima >= IntervaloMinimo;
    }
}