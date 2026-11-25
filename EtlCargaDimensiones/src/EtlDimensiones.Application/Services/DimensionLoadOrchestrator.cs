using EtlDimensiones.Domain.Entities;
using EtlDimensiones.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EtlDimensiones.Application.Services;

public sealed class DimensionLoadOrchestrator
{
    private readonly IEnumerable<IDimensionLoader> _loaders;
    private readonly ILogger<DimensionLoadOrchestrator> _logger;

    public DimensionLoadOrchestrator(
        IEnumerable<IDimensionLoader> loaders,
        ILogger<DimensionLoadOrchestrator> logger)
    {
        _loaders = loaders;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DimensionLoadResult>> RunAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DimensionLoadResult>();
        _logger.LogInformation("=================================================================");
        _logger.LogInformation("  INICIO DE CARGA DE DIMENSIONES -> DW_AnalisisVentas");
        _logger.LogInformation("=================================================================");

        foreach (var loader in _loaders)
        {
            _logger.LogInformation("Cargando {Dimension}...", loader.DimensionName);
            var result = await loader.LoadAsync(cancellationToken);
            results.Add(result);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[OK] {Dimension,-14} | Extraidos: {Ext,3} | Insertados: {Ins,3} | Actualizados: {Upd,3} | Sin cambios: {Skip,3} | {Ms} ms",
                    result.DimensionName,
                    result.Extracted,
                    result.Inserted,
                    result.Updated,
                    result.Skipped,
                    result.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogError(
                    "[ERROR] {Dimension} -> {Error}",
                    result.DimensionName,
                    result.ErrorMessage);
            }
        }

        _logger.LogInformation("=================================================================");
        _logger.LogInformation(
            "  CARGA FINALIZADA | Dimensiones exitosas: {Ok}/{Total}",
            results.Count(r => r.Success),
            results.Count);
        _logger.LogInformation("=================================================================");

        return results;
    }
}
