using EtlOpiniones.Domain.Entities;

namespace EtlOpiniones.Domain.Interfaces;

public interface IDataLoader
{
    Task<int> LoadToAnalyticsAsync(
        IReadOnlyCollection<StagingRecord> records,
        CancellationToken cancellationToken = default);
}
