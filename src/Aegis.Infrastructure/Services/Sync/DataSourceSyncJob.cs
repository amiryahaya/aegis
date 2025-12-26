using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Sync;

/// <summary>
/// Background job that performs data source synchronization
/// </summary>
public class DataSourceSyncJob
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IDataConnectorFactory _connectorFactory;
    private readonly ILogger<DataSourceSyncJob> _logger;

    public DataSourceSyncJob(
        IDataSourceRepository dataSourceRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IDataConnectorFactory connectorFactory,
        ILogger<DataSourceSyncJob> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _connectorFactory = connectorFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid dataSourceId, bool isFullSync)
    {
        _logger.LogInformation("Starting sync job for data source {DataSourceId}, Full: {IsFullSync}", dataSourceId, isFullSync);

        // Check if there's already a running sync
        var hasRunningSyncResult = await _syncHistoryRepository.HasRunningSyncAsync(dataSourceId);
        if (hasRunningSyncResult)
        {
            _logger.LogWarning("Sync already in progress for data source {DataSourceId}", dataSourceId);
            return;
        }

        // Get data source
        var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId);
        if (dataSource == null)
        {
            _logger.LogError("Data source {DataSourceId} not found", dataSourceId);
            return;
        }

        // Create sync history
        var syncType = isFullSync ? SyncType.Full : SyncType.Incremental;
        var syncHistory = SyncHistory.Create(dataSourceId, syncType);
        await _syncHistoryRepository.AddAsync(syncHistory);

        try
        {
            // Get connector
            var connectorResult = _connectorFactory.GetConnector(dataSource.Type);
            if (connectorResult.IsFailure)
            {
                syncHistory.MarkFailed($"Failed to get connector: {connectorResult.Error!.Message}");
                await _syncHistoryRepository.UpdateAsync(syncHistory);
                _logger.LogError("Failed to get connector for {DataSourceType}: {Error}", dataSource.Type, connectorResult.Error!.Message);
                return;
            }

            var connector = connectorResult.Value;

            // Get last sync cursor for incremental sync
            string? lastSyncCursor = null;
            if (!isFullSync)
            {
                var lastSuccessfulSync = await _syncHistoryRepository.GetLastSuccessfulSyncAsync(dataSourceId);
                lastSyncCursor = lastSuccessfulSync?.LastSyncCursor;
            }

            // Perform sync
            var syncOptions = new SyncOptions(
                IsFullSync: isFullSync,
                LastSyncCursor: lastSyncCursor,
                BatchSize: 100,
                ProgressCallback: progress =>
                {
                    _logger.LogInformation("Sync progress for {DataSourceId}: Processed={Processed}, Added={Added}, Updated={Updated}, Failed={Failed}",
                        dataSourceId, progress.Processed, progress.Added, progress.Updated, progress.Failed);

                    // Update progress in database periodically
                    syncHistory.UpdateProgress(progress.Added, progress.Updated, 0, progress.Failed);
                    _syncHistoryRepository.UpdateAsync(syncHistory).GetAwaiter().GetResult();
                });

            var syncResult = await connector.SyncAsync(dataSourceId, dataSource.Settings, syncOptions);

            if (syncResult.IsSuccess)
            {
                syncHistory.Complete(
                    syncResult.Value.DocumentsAdded,
                    syncResult.Value.DocumentsUpdated,
                    syncResult.Value.DocumentsDeleted,
                    syncResult.Value.DocumentsFailed,
                    syncResult.Value.NextCursor);

                await _syncHistoryRepository.UpdateAsync(syncHistory);

                _logger.LogInformation("Sync completed successfully for data source {DataSourceId}. Added: {Added}, Updated: {Updated}, Deleted: {Deleted}, Failed: {Failed}",
                    dataSourceId, syncResult.Value.DocumentsAdded, syncResult.Value.DocumentsUpdated, syncResult.Value.DocumentsDeleted, syncResult.Value.DocumentsFailed);
            }
            else
            {
                syncHistory.MarkFailed(syncResult.Error!.Message);
                await _syncHistoryRepository.UpdateAsync(syncHistory);

                _logger.LogError("Sync failed for data source {DataSourceId}: {Error}", dataSourceId, syncResult.Error!.Message);
            }
        }
        catch (Exception ex)
        {
            syncHistory.MarkFailed(ex.Message);
            await _syncHistoryRepository.UpdateAsync(syncHistory);

            _logger.LogError(ex, "Unexpected error during sync for data source {DataSourceId}", dataSourceId);
        }
    }
}
