namespace GestorIncidentesTI.Services;

/// <summary>
/// Hace el trabajo que necesita la base solo cuando alguien entra al
/// aplicativo: al abrir la página de inicio de sesión o al navegar con sesión
/// iniciada. La landing pública y las visitas anónimas no tocan la base, así
/// Azure SQL sin servidor puede pausarse y no consume la cuota gratuita.
/// - Siembra de roles y cuentas Admin: una vez por arranque del contenedor.
/// - Escalamientos de SLA: como máximo una vez cada 5 minutos.
/// </summary>
public class EscalamientoBajoDemandaMiddleware
{
    private const string RutaLogin = "/Identity/Account/Login";
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static long _ultimaEjecucionTicks;
    private static bool _siembraHecha;

    private readonly RequestDelegate _next;

    public EscalamientoBajoDemandaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISlaService slaService,
        IConfiguration configuration,
        ILogger<EscalamientoBajoDemandaMiddleware> logger)
    {
        if (HttpMethods.IsGet(context.Request.Method)
            && (context.User.Identity?.IsAuthenticated == true
                || context.Request.Path.StartsWithSegments(RutaLogin, StringComparison.OrdinalIgnoreCase))
            && TocaEvaluar()
            && await Candado.WaitAsync(0))
        {
            try
            {
                if (TocaEvaluar())
                {
                    if (!_siembraHecha)
                    {
                        await SiembraInicial.EjecutarAsync(context.RequestServices, configuration, logger);
                        _siembraHecha = true;
                    }

                    var escalados = await slaService.EvaluarEscalamientosAsync();

                    if (escalados > 0)
                        logger.LogInformation("Escalamiento bajo demanda: {Cantidad} incidente(s) escalado(s).", escalados);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en la siembra o evaluación de escalamientos bajo demanda.");
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
