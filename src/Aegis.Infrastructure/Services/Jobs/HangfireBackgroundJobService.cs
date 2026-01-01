using Aegis.Domain.Services;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Jobs;

/// <summary>
/// Hangfire-based implementation of IBackgroundJobService
/// </summary>
public class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<HangfireBackgroundJobService> _logger;
    private readonly JobStorage _jobStorage;

    public HangfireBackgroundJobService(
        IBackgroundJobClient backgroundJobClient,
        JobStorage jobStorage,
        ILogger<HangfireBackgroundJobService> logger)
    {
        _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
        _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Enqueue<T>(Guid jobId) where T : IBackgroundJob
    {
        _logger.LogInformation("Enqueueing job {JobType} with ID {JobId}", typeof(T).Name, jobId);

        var backgroundJobId = _backgroundJobClient.Enqueue<T>(
            job => job.ExecuteAsync(jobId, CancellationToken.None));

        _logger.LogInformation("Job {JobType} enqueued with Hangfire ID {BackgroundJobId}",
            typeof(T).Name, backgroundJobId);

        return backgroundJobId;
    }

    public string Schedule<T>(Guid jobId, TimeSpan delay) where T : IBackgroundJob
    {
        _logger.LogInformation("Scheduling job {JobType} with ID {JobId} for {Delay}",
            typeof(T).Name, jobId, delay);

        var backgroundJobId = _backgroundJobClient.Schedule<T>(
            job => job.ExecuteAsync(jobId, CancellationToken.None),
            delay);

        _logger.LogInformation("Job {JobType} scheduled with Hangfire ID {BackgroundJobId}",
            typeof(T).Name, backgroundJobId);

        return backgroundJobId;
    }

    public JobStatus GetJobStatus(string backgroundJobId)
    {
        using var connection = _jobStorage.GetConnection();
        var jobData = connection.GetJobData(backgroundJobId);

        if (jobData == null)
        {
            return JobStatus.Unknown;
        }

        return jobData.State switch
        {
            "Enqueued" => JobStatus.Enqueued,
            "Processing" => JobStatus.Processing,
            "Succeeded" => JobStatus.Succeeded,
            "Failed" => JobStatus.Failed,
            "Deleted" => JobStatus.Deleted,
            "Scheduled" => JobStatus.Scheduled,
            "Awaiting" => JobStatus.Awaiting,
            _ => JobStatus.Unknown
        };
    }

    public bool Cancel(string backgroundJobId)
    {
        try
        {
            var result = _backgroundJobClient.Delete(backgroundJobId);
            _logger.LogInformation("Job {BackgroundJobId} cancellation result: {Result}",
                backgroundJobId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {BackgroundJobId}", backgroundJobId);
            return false;
        }
    }

    public bool Retry(string backgroundJobId)
    {
        try
        {
            var result = _backgroundJobClient.Requeue(backgroundJobId);
            _logger.LogInformation("Job {BackgroundJobId} requeue result: {Result}",
                backgroundJobId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry job {BackgroundJobId}", backgroundJobId);
            return false;
        }
    }
}
