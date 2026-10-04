namespace GestorShopAPI.Services;

public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _cola;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(
        IBackgroundTaskQueue cola,
        IServiceProvider serviceProvider,
        ILogger<QueuedHostedService> logger)
    {
        _cola = cola;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de cola iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var tarea = await _cola.DesencolarAsync(stoppingToken);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await tarea(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando tarea en segundo plano");
            }
        }
    }
}
