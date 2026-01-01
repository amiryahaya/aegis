using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing background jobs
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Enqueues a job for immediate execution
    /// </summary>
    /// <typeparam name="T">The job type</typeparam>
    /// <param name="jobId">Unique identifier for the job</param>
    /// <returns>The background job ID</returns>
    string Enqueue<T>(Guid jobId) where T : IBackgroundJob;

    /// <summary>
    /// Schedules a job for delayed execution
    /// </summary>
    /// <typeparam name="T">The job type</typeparam>
    /// <param name="jobId">Unique identifier for the job</param>
    /// <param name="delay">Delay before execution</param>
    /// <returns>The background job ID</returns>
    string Schedule<T>(Guid jobId, TimeSpan delay) where T : IBackgroundJob;

    /// <summary>
    /// Gets the status of a background job
    /// </summary>
    /// <param name="backgroundJobId">The Hangfire job ID</param>
    /// <returns>The job status</returns>
    JobStatus GetJobStatus(string backgroundJobId);

    /// <summary>
    /// Cancels a pending or scheduled job
    /// </summary>
    /// <param name="backgroundJobId">The Hangfire job ID</param>
    /// <returns>True if cancelled successfully</returns>
    bool Cancel(string backgroundJobId);

    /// <summary>
    /// Retries a failed job
    /// </summary>
    /// <param name="backgroundJobId">The Hangfire job ID</param>
    /// <returns>True if requeued successfully</returns>
    bool Retry(string backgroundJobId);
}

/// <summary>
/// Base interface for all background jobs
/// </summary>
public interface IBackgroundJob
{
    /// <summary>
    /// Executes the job
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken);
}

/// <summary>
/// Status of a background job
/// </summary>
public enum JobStatus
{
    Unknown,
    Enqueued,
    Processing,
    Succeeded,
    Failed,
    Deleted,
    Scheduled,
    Awaiting
}

/// <summary>
/// Information about a background job
/// </summary>
public record JobInfo(
    string JobId,
    string JobType,
    JobStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Error,
    int RetryCount);
