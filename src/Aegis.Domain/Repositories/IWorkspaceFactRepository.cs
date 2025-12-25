using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface IWorkspaceFactRepository
{
    Task<WorkspaceFact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkspaceFact>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkspaceFact>> GetByConfidenceAsync(Guid workspaceId, string confidence, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(WorkspaceFact fact, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkspaceFact fact, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
