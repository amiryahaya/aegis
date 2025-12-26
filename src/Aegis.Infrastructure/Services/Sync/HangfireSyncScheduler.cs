using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Sync;

/// <summary>
/// Hangfire-based implementation of sync scheduler
/// </summary>
public class HangfireSyncScheduler : ISyncScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<HangfireSyncScheduler> _logger;

    public HangfireSyncScheduler(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager,
        ILogger<HangfireSyncScheduler> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    public Task<Result<string>> ScheduleRecurringSyncAsync(Guid dataSourceId, string cronExpression, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobId = $"sync-{dataSourceId}";

            _recurringJobManager.AddOrUpdate<DataSourceSyncJob>(
                jobId,
                job => job.ExecuteAsync(dataSourceId, false), // Incremental sync by default
                cronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            _logger.LogInformation("Scheduled recurring sync for data source {DataSourceId} with cron: {CronExpression}", dataSourceId, cronExpression);

            return Task.FromResult(Result<string>.Success(jobId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recurring sync for data source {DataSourceId}", dataSourceId);
            return Task.FromResult(Result<string>.Failure(Error.Internal("Scheduler.ScheduleFailed", ex.Message)));
        }
    }

    public Task<Result<string>> TriggerImmediateSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobId = _backgroundJobClient.Enqueue<DataSourceSyncJob>(
                job => job.ExecuteAsync(dataSourceId, false)); // Incremental sync

            _logger.LogInformation("Triggered immediate sync for data source {DataSourceId}, Job ID: {JobId}", dataSourceId, jobId);

            return Task.FromResult(Result<string>.Success(jobId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger immediate sync for data source {DataSourceId}", dataSourceId);
            return Task.FromResult(Result<string>.Failure(Error.Internal("Scheduler.TriggerFailed", ex.Message)));
        }
    }

    public Task<Result> CancelScheduledSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobId = $"sync-{dataSourceId}";
            _recurringJobManager.RemoveIfExists(jobId);

            _logger.LogInformation("Cancelled scheduled sync for data source {DataSourceId}", dataSourceId);

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel scheduled sync for data source {DataSourceId}", dataSourceId);
            return Task.FromResult(Result.Failure(Error.Internal("Scheduler.CancelFailed", ex.Message)));
        }
    }

    public Task<Result<SyncJobStatus>> GetSyncJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For recurring jobs, extract data source ID from job ID
            if (jobId.StartsWith("sync-"))
            {
                // Hangfire doesn't provide a direct API to get recurring job details easily
                // This is a simplified implementation
                var status = new SyncJobStatus(
                    JobId: jobId,
                    Status: "Scheduled",
                    NextRunTime: null,
                    LastRunTime: null);

                return Task.FromResult(Result<SyncJobStatus>.Success(status));
            }

            // For one-time jobs, we would need to query Hangfire storage
            // This is a simplified implementation
            var jobStatus = new SyncJobStatus(
                JobId: jobId,
                Status: "Unknown",
                NextRunTime: null,
                LastRunTime: null);

            return Task.FromResult(Result<SyncJobStatus>.Success(jobStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sync job status for {JobId}", jobId);
            return Task.FromResult(Result<SyncJobStatus>.Failure(Error.Internal("Scheduler.StatusFailed", ex.Message)));
        }
    }
}
