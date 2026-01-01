using System.Diagnostics;
using Aegis.Domain.Common;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Jobs;

/// <summary>
/// Interface for cleanup job runner (registered in DI)
/// </summary>
public interface ICleanupJobRunner
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Interface for data source sync runner (registered in DI)
/// </summary>
public interface IDataSourceSyncRunner
{
    Task CheckAndSyncDataSourcesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Background job for cleaning up expired data
/// </summary>
public class CleanupJob : ICleanupJob, ICleanupJobRunner
{
    private readonly ISemanticCache _semanticCache;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly IResponseCache _responseCache;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CleanupJob> _logger;

    public CleanupJob(
        ISemanticCache semanticCache,
        IEmbeddingCache embeddingCache,
        IResponseCache responseCache,
        IAuditLogService auditLogService,
        ILogger<CleanupJob> logger)
    {
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
        _embeddingCache = embeddingCache ?? throw new ArgumentNullException(nameof(embeddingCache));
        _responseCache = responseCache ?? throw new ArgumentNullException(nameof(responseCache));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900])]
    [Queue("low")]
    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// ICleanupJobRunner implementation for recurring job scheduling
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting cleanup job");

        var result = await CleanupAsync(cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Cleanup job failed: {Error}", result.Error?.Message);
            throw new InvalidOperationException($"Cleanup failed: {result.Error?.Message}");
        }

        _logger.LogInformation("Cleanup completed: {CacheEntries} cache entries, {AuditLogs} audit logs archived, {Chunks} orphaned chunks removed in {Time}",
            result.Value.ExpiredCacheEntriesRemoved,
            result.Value.OldAuditLogsArchived,
            result.Value.OrphanedChunksRemoved,
            result.Value.CleanupTime);
    }

    public async Task<Result<CleanupResult>> CleanupAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            int cacheEntriesRemoved = 0;
            int auditLogsArchived = 0;
            int orphanedChunksRemoved = 0;

            // Clean semantic cache
            _logger.LogDebug("Cleaning semantic cache...");
            var semanticCacheStats = await _semanticCache.GetStatsAsync(cancellationToken);
            if (semanticCacheStats.IsSuccess)
            {
                var evicted = await _semanticCache.EvictExpiredAsync(cancellationToken);
                if (evicted.IsSuccess)
                {
                    cacheEntriesRemoved += evicted.Value;
                    _logger.LogDebug("Evicted {Count} semantic cache entries", evicted.Value);
                }
            }

            // Clean embedding cache
            _logger.LogDebug("Cleaning embedding cache...");
            var embeddingCacheStats = await _embeddingCache.GetStatsAsync(cancellationToken);
            if (embeddingCacheStats.IsSuccess)
            {
                var evicted = await _embeddingCache.EvictExpiredAsync(cancellationToken);
                if (evicted.IsSuccess)
                {
                    cacheEntriesRemoved += evicted.Value;
                    _logger.LogDebug("Evicted {Count} embedding cache entries", evicted.Value);
                }
            }

            // Clean response cache
            _logger.LogDebug("Cleaning response cache...");
            var responseCacheStats = await _responseCache.GetStatsAsync(cancellationToken);
            if (responseCacheStats.IsSuccess)
            {
                var evicted = await _responseCache.EvictExpiredAsync(cancellationToken);
                if (evicted.IsSuccess)
                {
                    cacheEntriesRemoved += evicted.Value;
                    _logger.LogDebug("Evicted {Count} response cache entries", evicted.Value);
                }
            }

            // Archive old audit logs (older than 90 days)
            _logger.LogDebug("Archiving old audit logs...");
            var archiveBefore = DateTime.UtcNow.AddDays(-90);
            var archiveResult = await _auditLogService.ArchiveLogsBeforeAsync(archiveBefore, cancellationToken);
            if (archiveResult.IsSuccess)
            {
                auditLogsArchived = archiveResult.Value;
                _logger.LogDebug("Archived {Count} audit logs", auditLogsArchived);
            }

            // TODO: Clean up orphaned chunks (chunks without parent documents)
            // This would require a database query to find and remove orphaned records

            stopwatch.Stop();

            return Result<CleanupResult>.Success(new CleanupResult(
                cacheEntriesRemoved,
                auditLogsArchived,
                orphanedChunksRemoved,
                stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
            stopwatch.Stop();

            return Result<CleanupResult>.Failure(
                Error.Internal("Cleanup.Failed", ex.Message));
        }
    }
}

/// <summary>
/// Runner for data source sync checks
/// </summary>
public class DataSourceSyncRunner : IDataSourceSyncRunner
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ILogger<DataSourceSyncRunner> _logger;

    public DataSourceSyncRunner(
        IDataSourceRepository dataSourceRepository,
        IBackgroundJobService backgroundJobService,
        ILogger<DataSourceSyncRunner> logger)
    {
        _dataSourceRepository = dataSourceRepository ?? throw new ArgumentNullException(nameof(dataSourceRepository));
        _backgroundJobService = backgroundJobService ?? throw new ArgumentNullException(nameof(backgroundJobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CheckAndSyncDataSourcesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking data sources for sync...");

        var dataSources = await _dataSourceRepository.GetDueSyncAsync(cancellationToken);

        foreach (var dataSource in dataSources)
        {
            _logger.LogInformation("Enqueueing sync for data source {DataSourceId} ({Name})",
                dataSource.Id, dataSource.Name);

            _backgroundJobService.Enqueue<IDataSourceSyncJob>(dataSource.Id);
        }

        _logger.LogInformation("Enqueued {Count} data source sync jobs", dataSources.Count);
    }
}
