using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Document>> GetByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task<Document?> GetByExternalIdAsync(Guid dataSourceId, string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Document>> GetPendingDocumentsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetCountByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
}
