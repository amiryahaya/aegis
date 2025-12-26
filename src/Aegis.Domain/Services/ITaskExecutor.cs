using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Executes plans with task dependencies (DAG-based execution)
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Execute a plan
    /// </summary>
    Task<Result<PlanExecutionResult>> ExecutePlanAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a single task
    /// </summary>
    Task<Result<TaskResult>> ExecuteTaskAsync(
        PlanTask task,
        Dictionary<Guid, TaskResult> previousResults,
        CancellationToken cancellationToken = default);
}
