using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface IWorkspaceEntityRepository
{
    Task<WorkspaceEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkspaceEntity>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkspaceEntity>> GetByTypeAsync(Guid workspaceId, string type, CancellationToken cancellationToken = default);
    Task<WorkspaceEntity?> GetByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(WorkspaceEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkspaceEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
