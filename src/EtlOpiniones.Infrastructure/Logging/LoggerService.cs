using Microsoft.Extensions.Logging;

namespace EtlOpiniones.Infrastructure.Logging;

public sealed class LoggerService
{
    private readonly ILogger<LoggerService> _logger;

    public LoggerService(ILogger<LoggerService> logger)
    {
        _logger = logger;
    }

    public void LogMetric(string sourceName, int recordCount, long elapsedMs)
    {
        _logger.LogInformation(
            "[ETL-METRIC] Fuente={SourceName} | Registros={RecordCount} | DuracionMs={ElapsedMs}",
            sourceName,
            recordCount,
            elapsedMs);
    }

    public void LogFailure(string sourceName, Exception exception)
    {
        _logger.LogError(exception, "[ETL-ERROR] Fallo en fuente={SourceName}", sourceName);
    }
}
