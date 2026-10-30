using GestorIncidentesTI.Data;

namespace GestorIncidentesTI.Services;

/// <summary>
/// Hace el trabajo que necesita la base solo cuando alguien entra al
/// aplicativo: al abrir la página de inicio de sesión o al navegar con sesión
/// iniciada. La landing pública y las visitas anónimas no tocan la base, así
/// Azure SQL sin servidor puede pausarse y no consume la cuota gratuita.
/// - Preparación (una vez por arranque del contenedor): migraciones
///   pendientes, depuración de datos viejos de QA y siembra de roles y
///   cuentas Admin. Las peticiones esperan a que termine, para que ninguna
///   consulte columnas que la migración todavía no creó.
/// - Escalamientos de SLA: como máximo una vez cada 5 minutos.
/// </summary>
public class EscalamientoBajoDemandaMiddleware
{
    private const string RutaLogin = "/Identity/Account/Login";
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EsperaTrasFalloPreparacion = TimeSpan.FromMinutes(1);
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static readonly SemaphoreSlim CandadoPreparacion = new(1, 1);
    private static long _ultimaEjecucionTicks;
    private static long _ultimoFalloPreparacionTicks;
    private static volatile bool _preparada;

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
        var usaBase = HttpMethods.IsGet(context.Request.Method)
            ? context.User.Identity?.IsAuthenticated == true
              || context.Request.Path.StartsWithSegments(RutaLogin, StringComparison.OrdinalIgnoreCase)
            : context.User.Identity?.IsAuthenticated == true;

        if (usaBase && !_preparada)
            await PrepararAsync(context.RequestServices, configuration, logger);

        if (usaBase
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
                logger.LogError(ex, "Error en la evaluación de escalamientos bajo demanda.");
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

    private static async Task PrepararAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var ultimoFallo = new DateTime(Interlocked.Read(ref _ultimoFalloPreparacionTicks), DateTimeKind.Utc);
        if (DateTime.UtcNow - ultimoFallo < EsperaTrasFalloPreparacion) return;

        await CandadoPreparacion.WaitAsync();
        try
        {
            if (_preparada) return;

            var db = services.GetRequiredService<ApplicationDbContext>();
            await MantenimientoBase.MigrarAsync(db, logger);
            try { await MantenimientoBase.DepurarAsync(db, logger); }
            catch (Exception ex) { logger.LogError(ex, "Error en la depuración de datos de QA; se reintenta en el próximo arranque."); }
            await SiembraInicial.EjecutarAsync(services, configuration, logger);
            _preparada = true;
        }
        catch (Exception ex)
        {
            // Sin reintentar en cada petición mientras la base no responda.
            Interlocked.Exchange(ref _ultimoFalloPreparacionTicks, DateTime.UtcNow.Ticks);
            logger.LogError(ex, "Error preparando la base (migraciones, depuración o siembra).");
        }
        finally
        {
            CandadoPreparacion.Release();
        }
    }

    private static bool TocaEvaluar()
    {
        var ultima = new DateTime(Interlocked.Read(ref _ultimaEjecucionTicks), DateTimeKind.Utc);
        return DateTime.UtcNow - ultima >= IntervaloMinimo;
    }
}
