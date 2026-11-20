using System.Data;
using EtlOpiniones.Application.Options;
using EtlOpiniones.Domain.Entities;
using EtlOpiniones.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtlOpiniones.Infrastructure.Extractors;

public sealed class DatabaseExtractor : IExtractor
{
    private readonly DatabaseSourceOptions _options;
    private readonly string _connectionString;
    private readonly ILogger<DatabaseExtractor> _logger;

    public string SourceName => "SQL_ResenasRelacional";

    public DatabaseExtractor(
        IOptions<EtlSettings> settings,
        IConfiguration configuration,
        ILogger<DatabaseExtractor> logger)
    {
        _options = settings.Value.Database;
        _connectionString = configuration.GetConnectionString(_options.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"No se encontró la cadena de conexión '{_options.ConnectionStringName}'.");
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

        var records = new List<StagingRecord>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(_options.Query, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new StagingRecord
            {
                SourceSystem = SourceName,
                ExternalId = reader.GetInt32(0).ToString(),
                CustomerRef = reader.GetInt32(1).ToString(),
                ProductRef = reader.GetInt32(2).ToString(),
                EventDate = reader.GetDateTime(3),
                Rating = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Content = reader.IsDBNull(5) ? string.Empty : reader.GetString(5).Trim(),
                ExtractedAtUtc = DateTime.UtcNow
            });
        }

        return records;
    }
}
