using EtlDimensiones.Application.Services;
using EtlDimensiones.Domain.Interfaces;
using EtlDimensiones.Infrastructure.Loaders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Registro de los 6 cargadores de dimensiones (Open/Closed + Dependency Inversion)
builder.Services.AddScoped<IDimensionLoader, DimFuenteLoader>();
builder.Services.AddScoped<IDimensionLoader, DimUbicacionLoader>();
builder.Services.AddScoped<IDimensionLoader, DimVendedorLoader>();
builder.Services.AddScoped<IDimensionLoader, DimClienteLoader>();
builder.Services.AddScoped<IDimensionLoader, DimProductoLoader>();
builder.Services.AddScoped<IDimensionLoader, DimFechaLoader>();

builder.Services.AddScoped<DimensionLoadOrchestrator>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var orchestrator = scope.ServiceProvider.GetRequiredService<DimensionLoadOrchestrator>();
await orchestrator.RunAllAsync();
