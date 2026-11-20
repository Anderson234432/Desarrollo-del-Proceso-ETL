namespace EtlOpiniones.Application.Options;

public sealed class EtlSettings
{
    public const string SectionName = "EtlSettings";
    public int IntervalMinutes { get; set; } = 60;
    public string StagingDirectory { get; set; } = "staging";
    public CsvSourceOptions Csv { get; set; } = new();
    public DatabaseSourceOptions Database { get; set; } = new();
    public ApiSourceOptions Api { get; set; } = new();
}

public sealed class CsvSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string FilePath { get; set; } = "samples/encuestas_internas.csv";
    public string Delimiter { get; set; } = ",";
}

public sealed class DatabaseSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string ConnectionStringName { get; set; } = "OperationalDb";
    public string Query { get; set; } = "SELECT IdResena, IdCliente, IdProducto, Fecha, Calificacion, TextoResena FROM ResenasClientes WHERE Procesado = 0";
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class ApiSourceOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.ejemplo.local/";
    public string Endpoint { get; set; } = "v1/comentarios";
    public string ApiKeyHeader { get; set; } = "X-Api-Key";
    public int TimeoutSeconds { get; set; } = 30;
}
