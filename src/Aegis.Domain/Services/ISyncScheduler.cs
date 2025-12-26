using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Schedules and manages periodic sync jobs
/// </summary>
public interface ISyncScheduler
{
    /// <summary>
    /// Schedules a recurring sync job for a data source
    /// </summary>
    Task<Result<string>> ScheduleRecurringSyncAsync(Guid dataSourceId, string cronExpression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an immediate sync for a data source
    /// </summary>
    Task<Result<string>> TriggerImmediateSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled sync job
    /// </summary>
    Task<Result> CancelScheduledSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a sync job
    /// </summary>
    Task<Result<SyncJobStatus>> GetSyncJobStatusAsync(string jobId, CancellationToken cancellationToken = default);
}

public record SyncJobStatus(
    string JobId,
    string Status,
    DateTime? NextRunTime,
    DateTime? LastRunTime);
