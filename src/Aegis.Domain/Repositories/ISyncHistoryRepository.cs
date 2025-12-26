using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface ISyncHistoryRepository
{
    Task<SyncHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncHistory>> GetByDataSourceIdAsync(Guid dataSourceId, int limit = 50, CancellationToken cancellationToken = default);
    Task<SyncHistory?> GetLastSuccessfulSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task<SyncHistory?> GetRunningSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task AddAsync(SyncHistory syncHistory, CancellationToken cancellationToken = default);
    Task UpdateAsync(SyncHistory syncHistory, CancellationToken cancellationToken = default);
    Task<bool> HasRunningSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
}
