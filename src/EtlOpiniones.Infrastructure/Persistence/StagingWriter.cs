using System.Text.Json;
using EtlOpiniones.Application.Options;
using EtlOpiniones.Domain.Entities;
using EtlOpiniones.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace EtlOpiniones.Infrastructure.Persistence;

public sealed class StagingWriter : IStagingWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _stagingDirectory;

    public StagingWriter(IOptions<EtlSettings> settings)
    {
        _stagingDirectory = settings.Value.StagingDirectory;
    }

    public async Task<string> WriteAsync(
        string sourceName,
        IReadOnlyCollection<StagingRecord> records,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_stagingDirectory);

        var fileName = $"{sourceName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var fullPath = Path.Combine(_stagingDirectory, fileName);

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, records, JsonOptions, cancellationToken);

        return fullPath;
    }
}
