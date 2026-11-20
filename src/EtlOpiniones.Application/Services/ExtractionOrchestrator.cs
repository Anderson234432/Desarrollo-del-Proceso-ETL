using System.Diagnostics;
using EtlOpiniones.Application.DTOs;
using EtlOpiniones.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EtlOpiniones.Application.Services;

public sealed class ExtractionOrchestrator
{
    private readonly IEnumerable<IExtractor> _extractors;
    private readonly IStagingWriter _stagingWriter;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        IEnumerable<IExtractor> extractors,
        IStagingWriter stagingWriter,
        ILogger<ExtractionOrchestrator> logger)
    {
        _extractors = extractors;
        _stagingWriter = stagingWriter;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ExtractionResult>> RunExtractionCycleAsync(
        CancellationToken cancellationToken = default)
    {
        var globalWatch = Stopwatch.StartNew();
        _logger.LogInformation("Iniciando ciclo paralelo de extracción ETL.");

        var tasks = _extractors.Select(extractor => ExecuteSingleExtractorAsync(extractor, cancellationToken));
        var results = await Task.WhenAll(tasks);

        globalWatch.Stop();
        var totalRecords = results.Sum(r => r.RecordsExtracted);
        _logger.LogInformation(
            "Ciclo de extracción finalizado en {ElapsedMs} ms. Total de registros extraídos: {TotalRecords}.",
            globalWatch.ElapsedMilliseconds,
            totalRecords);

        return results;
    }

    private async Task<ExtractionResult> ExecuteSingleExtractorAsync(
        IExtractor extractor,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Iniciando extracción desde fuente: {SourceName}", extractor.SourceName);

            var records = await extractor.ExtractAsync(cancellationToken);
            var stagingPath = await _stagingWriter.WriteAsync(extractor.SourceName, records, cancellationToken);

            sw.Stop();
            _logger.LogInformation(
                "Fuente {SourceName} completada: {Count} registros en {ElapsedMs} ms. Staging: {StagingPath}",
                extractor.SourceName,
                records.Count,
                sw.ElapsedMilliseconds,
                stagingPath);

            return new ExtractionResult
            {
                SourceName = extractor.SourceName,
                RecordsExtracted = records.Count,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                StagingLocation = stagingPath,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Error extrayendo datos de la fuente {SourceName} tras {ElapsedMs} ms.",
                extractor.SourceName,
                sw.ElapsedMilliseconds);

            return new ExtractionResult
            {
                SourceName = extractor.SourceName,
                RecordsExtracted = 0,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
