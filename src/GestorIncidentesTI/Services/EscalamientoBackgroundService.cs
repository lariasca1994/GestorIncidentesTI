namespace GestorIncidentesTI.Services;

/// <summary>
/// Corre en segundo plano dentro de la misma app (sin infraestructura extra)
/// y revisa cada 5 minutos si algún incidente necesita escalar. Para producción
/// real esto normalmente sería un job separado, pero para un proyecto de
/// portafolio en un solo App Service, un BackgroundService es honesto y suficiente.
/// </summary>
public class EscalamientoBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EscalamientoBackgroundService> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    public EscalamientoBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<EscalamientoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var slaService = scope.ServiceProvider.GetRequiredService<ISlaService>();
                var escalados = await slaService.EvaluarEscalamientosAsync();

                if (escalados > 0)
                    _logger.LogInformation("Escalamiento automático: {Cantidad} incidente(s) escalado(s).", escalados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluando escalamientos automáticos.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
