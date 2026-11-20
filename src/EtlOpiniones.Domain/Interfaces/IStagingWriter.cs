using EtlOpiniones.Domain.Entities;

namespace EtlOpiniones.Domain.Interfaces;

public interface IStagingWriter
{
    Task<string> WriteAsync(
        string sourceName,
        IReadOnlyCollection<StagingRecord> records,
        CancellationToken cancellationToken = default);
}
