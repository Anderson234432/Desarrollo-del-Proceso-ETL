using EtlOpiniones.Domain.Entities;

namespace EtlOpiniones.Domain.Interfaces;

public interface IExtractor
{
    string SourceName { get; }
    Task<IReadOnlyCollection<StagingRecord>> ExtractAsync(CancellationToken cancellationToken = default);
}
