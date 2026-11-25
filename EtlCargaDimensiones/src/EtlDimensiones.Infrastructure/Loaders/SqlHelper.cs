using Microsoft.Extensions.Configuration;

namespace EtlDimensiones.Infrastructure.Loaders;

internal static class SqlHelper
{
    public static string GetRequiredConnection(IConfiguration config, string name)
        => config.GetConnectionString(name)
           ?? throw new InvalidOperationException($"No se encontró la cadena de conexión '{name}' en appsettings.json.");

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static object ToDbNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
