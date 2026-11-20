using EtlOpiniones.Domain.Entities;
using EtlOpiniones.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EtlOpiniones.Infrastructure.Persistence;

public sealed class DataLoader : IDataLoader
{
    private readonly ILogger<DataLoader> _logger;

    public DataLoader(ILogger<DataLoader> logger)
    {
        _logger = logger;
    }

    public Task<int> LoadToAnalyticsAsync(
        IReadOnlyCollection<StagingRecord> records,
        CancellationToken cancellationToken = default)
    {
        // Punto de extensión para la fase Load (L) hacia la Base de Datos Analítica (Data Warehouse).
        _logger.LogInformation("Preparados {Count} registros de staging para carga analítica.", records.Count);
        return Task.FromResult(records.Count);
    }
}
