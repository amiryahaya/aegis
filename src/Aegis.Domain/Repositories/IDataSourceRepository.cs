using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface IDataSourceRepository
{
    Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataSource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataSource>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataSource>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task AddAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsInTeamAsync(string name, Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get data sources that are due for sync (nextSyncAt <= now)
    /// </summary>
    Task<IReadOnlyList<DataSource>> GetDueSyncAsync(CancellationToken cancellationToken = default);
}
