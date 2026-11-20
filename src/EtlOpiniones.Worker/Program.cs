using EtlOpiniones.Application.Options;
using EtlOpiniones.Application.Services;
using EtlOpiniones.Domain.Interfaces;
using EtlOpiniones.Infrastructure.Extractors;
using EtlOpiniones.Infrastructure.Logging;
using EtlOpiniones.Infrastructure.Persistence;
using EtlOpiniones.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Configuración modular desde appsettings.json, variables de entorno y User Secrets
builder.Services.Configure<EtlSettings>(
    builder.Configuration.GetSection(EtlSettings.SectionName));

// Registro de IHttpClientFactory con timeout y cabecera segura
builder.Services.AddHttpClient(ApiExtractor.HttpClientName, (sp, client) =>
{
    var config = builder.Configuration;
    var baseUrl = config["EtlSettings:Api:BaseUrl"] ?? "https://api.ejemplo.local/";
    var timeout = int.TryParse(config["EtlSettings:Api:TimeoutSeconds"], out var t) ? t : 30;
    var apiKeyHeader = config["EtlSettings:Api:ApiKeyHeader"] ?? "X-Api-Key";
    var apiKey = config["Secrets:ComentariosApiKey"];

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeout);
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Add(apiKeyHeader, apiKey);
    }
});

// Registro polimórfico de extractores (Principio Open/Closed y Strategy)
builder.Services.AddScoped<IExtractor, CsvExtractor>();
builder.Services.AddScoped<IExtractor, DatabaseExtractor>();
builder.Services.AddScoped<IExtractor, ApiExtractor>();

// Persistencia Staging, Carga y Orquestación
builder.Services.AddScoped<IStagingWriter, StagingWriter>();
builder.Services.AddScoped<IDataLoader, DataLoader>();
builder.Services.AddScoped<LoggerService>();
builder.Services.AddScoped<ExtractionOrchestrator>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
