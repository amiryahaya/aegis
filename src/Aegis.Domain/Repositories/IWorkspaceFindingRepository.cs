using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface IWorkspaceFindingRepository
{
    Task<WorkspaceFinding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkspaceFinding>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkspaceFinding>> GetByTypeAsync(Guid workspaceId, string type, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(WorkspaceFinding finding, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkspaceFinding finding, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
