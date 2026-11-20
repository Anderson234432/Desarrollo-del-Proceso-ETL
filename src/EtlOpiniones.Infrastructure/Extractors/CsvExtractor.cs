using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using EtlOpiniones.Application.Options;
using EtlOpiniones.Domain.Entities;
using EtlOpiniones.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtlOpiniones.Infrastructure.Extractors;

public sealed class CsvExtractor : IExtractor
{
    private readonly CsvSourceOptions _options;
    private readonly ILogger<CsvExtractor> _logger;

    public string SourceName => "CSV_EncuestasInternas";

    public CsvExtractor(IOptions<EtlSettings> settings, ILogger<CsvExtractor> logger)
    {
        _options = settings.Value.Csv;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<StagingRecord>> ExtractAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Fuente {SourceName} deshabilitada en configuración.", SourceName);
            return Array.Empty<StagingRecord>();
        }

        if (!File.Exists(_options.FilePath))
        {
            throw new FileNotFoundException($"No se encontró el archivo CSV en: {_options.FilePath}");
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = _options.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = context =>
                _logger.LogWarning("Fila CSV inválida en línea {Row}: {Raw}", context.Context.Parser.Row, context.RawRecord)
        };

        var records = new List<StagingRecord>();
        using var reader = new StreamReader(_options.FilePath);
        using var csv = new CsvReader(reader, config);

        await foreach (var row in csv.GetRecordsAsync<EncuestaInterna>(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(row.IdEncuesta))
                continue;

            records.Add(new StagingRecord
            {
                SourceSystem = SourceName,
                ExternalId = row.IdEncuesta.Trim(),
                CustomerRef = row.IdCliente.Trim(),
                ProductRef = row.IdProducto.Trim(),
                EventDate = row.Fecha,
                Rating = row.PuntajeSatisfaccion,
                Content = row.Comentario.Trim(),
                ExtractedAtUtc = DateTime.UtcNow
            });
        }

        return records;
    }
}
