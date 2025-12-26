using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Agent responsible for decomposing complex queries into executable plans
/// </summary>
public interface IPlannerAgent : IAgent
{
    /// <summary>
    /// Create an execution plan for a given query
    /// </summary>
    Task<Result<ExecutionPlan>> CreatePlanAsync(
        string query,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An execution plan with tasks and dependencies
/// </summary>
public record ExecutionPlan
{
    /// <summary>
    /// Unique plan identifier
    /// </summary>
    public required Guid PlanId { get; init; }

    /// <summary>
    /// Original query that generated this plan
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Tasks in the plan
    /// </summary>
    public required List<PlanTask> Tasks { get; init; }

    /// <summary>
    /// Expected execution time estimate
    /// </summary>
    public TimeSpan? EstimatedDuration { get; init; }
}

/// <summary>
/// A single task in an execution plan
/// </summary>
public record PlanTask
{
    /// <summary>
    /// Unique task identifier
    /// </summary>
    public required Guid TaskId { get; init; }

    /// <summary>
    /// Task description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Agent type that should execute this task
    /// </summary>
    public required string AgentType { get; init; }

    /// <summary>
    /// Plugin to invoke (if applicable)
    /// </summary>
    public string? PluginName { get; init; }

    /// <summary>
    /// Function to invoke (if applicable)
    /// </summary>
    public string? FunctionName { get; init; }

    /// <summary>
    /// Task IDs that must complete before this task can start
    /// </summary>
    public List<Guid> Dependencies { get; init; } = new();

    /// <summary>
    /// Priority (higher = more important)
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Parameters for the task
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Result of executing a plan
/// </summary>
public record PlanExecutionResult
{
    /// <summary>
    /// Plan ID that was executed
    /// </summary>
    public required Guid PlanId { get; init; }

    /// <summary>
    /// Whether the plan completed successfully
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Results from each task
    /// </summary>
    public required Dictionary<Guid, TaskResult> TaskResults { get; init; }

    /// <summary>
    /// Final synthesized result
    /// </summary>
    public string? FinalResult { get; init; }

    /// <summary>
    /// Execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }
}

/// <summary>
/// Result of a single task execution
/// </summary>
public record TaskResult
{
    /// <summary>
    /// Task ID
    /// </summary>
    public required Guid TaskId { get; init; }

    /// <summary>
    /// Whether the task succeeded
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Task output
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }
}
