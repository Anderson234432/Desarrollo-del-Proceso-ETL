namespace EtlOpiniones.Application.DTOs;

public sealed class ExtractionResult
{
    public string SourceName { get; init; } = string.Empty;
    public int RecordsExtracted { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public string StagingLocation { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
