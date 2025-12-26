using System.Diagnostics;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// DAG-based task executor for executing plans
/// </summary>
public class TaskExecutor : ITaskExecutor
{
    private readonly Dictionary<string, IAgent> _agents;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(
        Dictionary<string, IAgent> agents,
        ILogger<TaskExecutor> logger)
    {
        _agents = agents;
        _logger = logger;
    }

    public async Task<Result<PlanExecutionResult>> ExecutePlanAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Executing plan {PlanId} with {TaskCount} tasks",
                plan.PlanId, plan.Tasks.Count);

            var taskResults = new Dictionary<Guid, TaskResult>();

            // If no tasks, return success
            if (plan.Tasks.Count == 0)
            {
                return Result<PlanExecutionResult>.Success(new PlanExecutionResult
                {
                    PlanId = plan.PlanId,
                    Success = true,
                    TaskResults = taskResults,
                    ExecutionTime = stopwatch.Elapsed
                });
            }

            // Get execution order (topological sort)
            var executionOrder = GetExecutionOrder(plan.Tasks);

            // Execute tasks in order
            foreach (var task in executionOrder)
            {
                _logger.LogInformation("Executing task {TaskId}: {Description}",
                    task.TaskId, task.Description);

                var taskResult = await ExecuteTaskAsync(task, taskResults, cancellationToken);

                if (taskResult.IsFailure)
                {
                    return Result<PlanExecutionResult>.Failure(taskResult.Error!);
                }

                taskResults[task.TaskId] = taskResult.Value;

                if (!taskResult.Value.Success)
                {
                    _logger.LogWarning("Task {TaskId} failed: {Error}",
                        task.TaskId, taskResult.Value.ErrorMessage);

                    return Result<PlanExecutionResult>.Success(new PlanExecutionResult
                    {
                        PlanId = plan.PlanId,
                        Success = false,
                        TaskResults = taskResults,
                        ExecutionTime = stopwatch.Elapsed
                    });
                }
            }

            stopwatch.Stop();

            var result = new PlanExecutionResult
            {
                PlanId = plan.PlanId,
                Success = true,
                TaskResults = taskResults,
                ExecutionTime = stopwatch.Elapsed
            };

            _logger.LogInformation(
                "Plan {PlanId} completed successfully in {Duration}ms",
                plan.PlanId, stopwatch.ElapsedMilliseconds);

            return Result<PlanExecutionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing plan {PlanId}", plan.PlanId);
            return Result<PlanExecutionResult>.Failure(
                Error.Internal("TaskExecutor.Error", ex.Message));
        }
    }

    public async Task<Result<TaskResult>> ExecuteTaskAsync(
        PlanTask task,
        Dictionary<Guid, TaskResult> previousResults,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check if agent exists
            if (!_agents.TryGetValue(task.AgentType, out var agent))
            {
                return Result<TaskResult>.Failure(
                    Error.NotFound("Agent.NotFound", $"Agent type '{task.AgentType}' not found"));
            }

            // Build context from previous results
            var context = BuildContext(task, previousResults);

            // Create agent request
            var request = new AgentRequest
            {
                RequestId = task.TaskId,
                Task = task.Description,
                Context = context,
                WorkspaceId = task.Parameters.TryGetValue("workspaceId", out var wsId)
                    ? (Guid)wsId
                    : null
            };

            // Execute agent
            var response = await agent.ExecuteAsync(request, cancellationToken);

            stopwatch.Stop();

            if (response.IsFailure)
            {
                return Result<TaskResult>.Success(new TaskResult
                {
                    TaskId = task.TaskId,
                    Success = false,
                    ErrorMessage = response.Error?.Message ?? "Unknown error",
                    ExecutionTime = stopwatch.Elapsed
                });
            }

            return Result<TaskResult>.Success(new TaskResult
            {
                TaskId = task.TaskId,
                Success = true,
                Output = response.Value.Result,
                ExecutionTime = stopwatch.Elapsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task {TaskId}", task.TaskId);

            return Result<TaskResult>.Success(new TaskResult
            {
                TaskId = task.TaskId,
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = stopwatch.Elapsed
            });
        }
    }

    private List<PlanTask> GetExecutionOrder(List<PlanTask> tasks)
    {
        // Topological sort using Kahn's algorithm
        var order = new List<PlanTask>();
        var inDegree = new Dictionary<Guid, int>();
        var taskMap = tasks.ToDictionary(t => t.TaskId);

        // Calculate in-degrees (number of dependencies each task has)
        foreach (var task in tasks)
        {
            inDegree[task.TaskId] = task.Dependencies.Count;
        }

        // Find tasks with no dependencies
        var queue = new Queue<PlanTask>();
        foreach (var task in tasks)
        {
            if (inDegree[task.TaskId] == 0)
            {
                queue.Enqueue(task);
            }
        }

        // Process queue
        while (queue.Count > 0)
        {
            var task = queue.Dequeue();
            order.Add(task);

            // Reduce in-degree for tasks that depend on this task
            foreach (var otherTask in tasks)
            {
                if (otherTask.Dependencies.Contains(task.TaskId))
                {
                    inDegree[otherTask.TaskId]--;
                    if (inDegree[otherTask.TaskId] == 0)
                    {
                        queue.Enqueue(otherTask);
                    }
                }
            }
        }

        // Sort by priority within each level
        return order.OrderBy(t => -t.Priority).ToList();
    }

    private Dictionary<string, object> BuildContext(
        PlanTask task,
        Dictionary<Guid, TaskResult> previousResults)
    {
        var context = new Dictionary<string, object>(task.Parameters);

        // Add results from dependencies
        foreach (var depId in task.Dependencies)
        {
            if (previousResults.TryGetValue(depId, out var result))
            {
                context[$"dependency_{depId}"] = result.Output ?? "";
            }
        }

        return context;
    }
}
