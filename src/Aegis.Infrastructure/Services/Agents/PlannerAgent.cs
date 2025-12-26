using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Planner agent that decomposes complex queries into executable plans
/// </summary>
public class PlannerAgent : IPlannerAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<PlannerAgent> _logger;

    public string AgentType => "Planner";

    public PlannerAgent(
        Kernel kernel,
        ILogger<PlannerAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<Result<ExecutionPlan>> CreatePlanAsync(
        string query,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Result<ExecutionPlan>.Failure(
                    Error.Validation("Planner.EmptyQuery", "Query cannot be empty"));
            }

            _logger.LogInformation("Creating plan for query: {Query}", query);

            // Analyze query and decompose into tasks
            var tasks = AnalyzeQueryAndCreateTasks(query, workspaceId);

            var plan = new ExecutionPlan
            {
                PlanId = Guid.NewGuid(),
                Query = query,
                Tasks = tasks,
                EstimatedDuration = TimeSpan.FromSeconds(tasks.Count * 2) // Simple estimate
            };

            _logger.LogInformation(
                "Created plan {PlanId} with {TaskCount} tasks",
                plan.PlanId, tasks.Count);

            return Result<ExecutionPlan>.Success(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan for query: {Query}", query);
            return Result<ExecutionPlan>.Failure(
                Error.Internal("Planner.Error", ex.Message));
        }
    }

    public async Task<Result<AgentResponse>> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workspaceId = request.WorkspaceId ?? Guid.Empty;
            var planResult = await CreatePlanAsync(request.Task, workspaceId, cancellationToken);

            if (planResult.IsFailure)
            {
                return Result<AgentResponse>.Failure(planResult.Error!);
            }

            var plan = planResult.Value;

            var response = new AgentResponse
            {
                Result = JsonSerializer.Serialize(plan, new JsonSerializerOptions
                {
                    WriteIndented = true
                }),
                Confidence = 0.9,
                Metadata = new Dictionary<string, object>
                {
                    { "planId", plan.PlanId },
                    { "taskCount", plan.Tasks.Count }
                },
                ReasoningSteps = new List<string>
                {
                    $"Analyzed query: '{request.Task}'",
                    $"Identified {plan.Tasks.Count} tasks to execute",
                    "Generated execution plan with dependencies"
                }
            };

            return Result<AgentResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing planner agent");
            return Result<AgentResponse>.Failure(
                Error.Internal("Planner.ExecutionError", ex.Message));
        }
    }

    private List<PlanTask> AnalyzeQueryAndCreateTasks(string query, Guid workspaceId)
    {
        var tasks = new List<PlanTask>();
        var queryLower = query.ToLowerInvariant();

        // Detect search/retrieval needs
        if (ContainsSearchKeywords(queryLower))
        {
            tasks.Add(CreateSearchTask(query, workspaceId));
        }

        // Detect entity/graph needs
        if (ContainsEntityKeywords(queryLower))
        {
            var entityTask = CreateEntityLookupTask(query, workspaceId);

            // Add dependency if search task exists
            if (tasks.Count > 0)
            {
                entityTask = entityTask with { Dependencies = new List<Guid> { tasks[0].TaskId } };
            }

            tasks.Add(entityTask);
        }

        // Detect analysis needs
        if (ContainsAnalysisKeywords(queryLower))
        {
            var analysisTask = CreateAnalysisTask(query, workspaceId);

            // Analysis depends on previous tasks
            if (tasks.Count > 0)
            {
                analysisTask = analysisTask with
                {
                    Dependencies = tasks.Select(t => t.TaskId).ToList()
                };
            }

            tasks.Add(analysisTask);
        }

        // Detect timeline/temporal needs
        if (ContainsTemporalKeywords(queryLower))
        {
            var timelineTask = CreateTimelineTask(query, workspaceId);

            // Timeline depends on entity lookup if it exists
            var entityTaskId = tasks.FirstOrDefault(t => t.AgentType == "EntityLookup")?.TaskId;
            if (entityTaskId.HasValue)
            {
                timelineTask = timelineTask with { Dependencies = new List<Guid> { entityTaskId.Value } };
            }

            tasks.Add(timelineTask);
        }

        // If no specific tasks detected, create a general retrieval task
        if (tasks.Count == 0)
        {
            tasks.Add(CreateRetrievalTask(query, workspaceId));
        }

        // Always add a synthesis task at the end
        var synthesisTask = CreateSynthesisTask(query, workspaceId);
        synthesisTask = synthesisTask with
        {
            Dependencies = tasks.Select(t => t.TaskId).ToList()
        };
        tasks.Add(synthesisTask);

        return tasks;
    }

    private bool ContainsSearchKeywords(string query)
    {
        var keywords = new[] { "find", "search", "look for", "locate", "documents", "files" };
        return keywords.Any(kw => query.Contains(kw));
    }

    private bool ContainsEntityKeywords(string query)
    {
        var keywords = new[] { "person", "organization", "company", "entity", "entities", "who is", "what is" };
        return keywords.Any(kw => query.Contains(kw));
    }

    private bool ContainsAnalysisKeywords(string query)
    {
        var keywords = new[] { "analyze", "analysis", "pattern", "trend", "insight", "examine", "investigate" };
        return keywords.Any(kw => query.Contains(kw));
    }

    private bool ContainsTemporalKeywords(string query)
    {
        var keywords = new[] { "timeline", "history", "when", "chronological", "sequence", "events", "over time" };
        var hasYear = Regex.IsMatch(query, @"\b20\d{2}\b"); // Matches years like 2024
        return keywords.Any(kw => query.Contains(kw)) || hasYear;
    }

    private PlanTask CreateSearchTask(string query, Guid workspaceId)
    {
        return new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = $"Search for relevant documents: {query}",
            AgentType = "Retriever",
            PluginName = "VectorSearchPlugin",
            FunctionName = "VectorSearchAsync",
            Priority = 1,
            Parameters = new Dictionary<string, object>
            {
                { "query", query },
                { "workspaceId", workspaceId },
                { "topK", 10 }
            }
        };
    }

    private PlanTask CreateEntityLookupTask(string query, Guid workspaceId)
    {
        // Extract potential entity names from query
        var entityName = ExtractEntityName(query);

        return new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = $"Look up entity information: {entityName}",
            AgentType = "Retriever",
            PluginName = "EntityLookupPlugin",
            FunctionName = "LookupEntityAsync",
            Priority = 1,
            Parameters = new Dictionary<string, object>
            {
                { "entityName", entityName },
                { "workspaceId", workspaceId }
            }
        };
    }

    private PlanTask CreateAnalysisTask(string query, Guid workspaceId)
    {
        return new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = $"Analyze retrieved information: {query}",
            AgentType = "Analyzer",
            Priority = 2,
            Parameters = new Dictionary<string, object>
            {
                { "query", query },
                { "workspaceId", workspaceId }
            }
        };
    }

    private PlanTask CreateTimelineTask(string query, Guid workspaceId)
    {
        var entityName = ExtractEntityName(query);

        return new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = $"Build timeline for: {entityName}",
            AgentType = "Retriever",
            PluginName = "TimelineBuilderPlugin",
            FunctionName = "BuildTimelineAsync",
            Priority = 1,
            Parameters = new Dictionary<string, object>
            {
                { "entityName", entityName },
                { "workspaceId", workspaceId }
            }
        };
    }

    private PlanTask CreateRetrievalTask(string query, Guid workspaceId)
    {
        return new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = $"Retrieve information: {query}",
            AgentType = "Retriever",
            Priority = 1,
            Parameters = new Dictionary<string, object>
            {
                { "query", query },
                { "workspaceId", workspaceId }
            }
        };
    }

    private PlanTask CreateSynthesisTask(string query, Guid workspaceId)
    {
        return new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = $"Synthesize final response: {query}",
            AgentType = "Synthesizer",
            Priority = 10,
            Parameters = new Dictionary<string, object>
            {
                { "query", query },
                { "workspaceId", workspaceId }
            }
        };
    }

    private string ExtractEntityName(string query)
    {
        // Simple extraction - look for quoted names or capitalized words
        var quotedMatch = Regex.Match(query, @"""([^""]+)""");
        if (quotedMatch.Success)
        {
            return quotedMatch.Groups[1].Value;
        }

        // Look for capitalized words (potential names)
        var capitalizedMatch = Regex.Match(query, @"\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)\b");
        if (capitalizedMatch.Success)
        {
            return capitalizedMatch.Groups[1].Value;
        }

        return "entity";
    }
}
