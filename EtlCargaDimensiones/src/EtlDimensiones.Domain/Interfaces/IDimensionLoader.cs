using EtlDimensiones.Domain.Entities;

namespace EtlDimensiones.Domain.Interfaces;

public interface IDimensionLoader
{
    string DimensionName { get; }
    Task<DimensionLoadResult> LoadAsync(CancellationToken cancellationToken = default);
}
