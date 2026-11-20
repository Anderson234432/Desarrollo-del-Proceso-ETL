using EtlOpiniones.Application.Options;
using EtlOpiniones.Application.Services;
using Microsoft.Extensions.Options;

namespace EtlOpiniones.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EtlSettings _settings;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IServiceScopeFactory scopeFactory,
        IOptions<EtlSettings> settings,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ETL Extraction Worker iniciado a las: {Time}", DateTimeOffset.Now);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ExtractionOrchestrator>();

            var results = await orchestrator.RunExtractionCycleAsync(stoppingToken);
            foreach (var r in results)
            {
                _logger.LogInformation(
                    "Resumen [{Source}] -> Exito={Success}, Registros={Count}, Tiempo={Ms} ms",
                    r.SourceName, r.Success, r.RecordsExtracted, r.ElapsedMilliseconds);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
