using System.Net.Http.Json;
using EtlOpiniones.Application.Options;
using EtlOpiniones.Domain.Entities;
using EtlOpiniones.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtlOpiniones.Infrastructure.Extractors;

public sealed class ApiExtractor : IExtractor
{
    public const string HttpClientName = "ComentariosApiClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiSourceOptions _options;
    private readonly ILogger<ApiExtractor> _logger;

    public string SourceName => "REST_ComentariosApi";

    public ApiExtractor(
        IHttpClientFactory httpClientFactory,
        IOptions<EtlSettings> settings,
        ILogger<ApiExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = settings.Value.Api;
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

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(_options.Endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var comentarios = await response.Content.ReadFromJsonAsync<List<ComentarioApi>>(cancellationToken: cancellationToken)
                          ?? new List<ComentarioApi>();

        return comentarios.Select(c => new StagingRecord
        {
            SourceSystem = SourceName,
            ExternalId = c.Id,
            CustomerRef = c.Author,
            ProductRef = c.ProductCode,
            EventDate = c.CreatedAt,
            Rating = null,
            Content = c.Body.Trim(),
            ExtractedAtUtc = DateTime.UtcNow
        }).ToList();
    }
}
