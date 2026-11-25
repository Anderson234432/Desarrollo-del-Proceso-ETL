namespace EtlDimensiones.Domain.Entities;

public sealed record DimensionLoadResult(
    string DimensionName,
    int Extracted,
    int Inserted,
    int Updated,
    int Skipped,
    long ElapsedMilliseconds,
    bool Success,
    string? ErrorMessage = null);
