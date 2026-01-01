using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Service for logging and tracking reasoning steps across agents
/// </summary>
public class ReasoningTraceLogger : IReasoningTraceLogger
{
    private readonly ILogger<ReasoningTraceLogger> _logger;
    private readonly ConcurrentDictionary<Guid, ReasoningTrace> _traces = new();
    private readonly object _lockObject = new();

    public ReasoningTraceLogger(ILogger<ReasoningTraceLogger> logger)
    {
        _logger = logger;
    }

    public Guid StartTrace(Guid requestId, string query)
    {
        var traceId = Guid.NewGuid();
        var trace = new ReasoningTrace
        {
            TraceId = traceId,
            RequestId = requestId,
            Query = query,
            StartTime = DateTime.UtcNow
        };

        _traces[traceId] = trace;

        _logger.LogDebug("Started reasoning trace {TraceId} for request {RequestId}",
            traceId, requestId);

        return traceId;
    }

    public void AddStep(Guid traceId, string agentName, string description,
        Dictionary<string, object>? metadata = null)
    {
        if (!_traces.TryGetValue(traceId, out var trace))
        {
            _logger.LogWarning("Attempted to add step to non-existent trace {TraceId}", traceId);
            return;
        }

        lock (_lockObject)
        {
            var step = new ReasoningStep
            {
                StepNumber = trace.Steps.Count + 1,
                AgentName = agentName,
                Description = description,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            trace.Steps.Add(step);
        }

        _logger.LogDebug("Trace {TraceId} - {Agent}: {Description}",
            traceId, agentName, description);
    }

    public void AddDecision(Guid traceId, string agentName, string decisionType,
        string[] options, string selectedOption, string rationale)
    {
        if (!_traces.TryGetValue(traceId, out var trace))
        {
            _logger.LogWarning("Attempted to add decision to non-existent trace {TraceId}", traceId);
            return;
        }

        lock (_lockObject)
        {
            var decision = new DecisionPoint
            {
                AgentName = agentName,
                DecisionType = decisionType,
                Options = options,
                SelectedOption = selectedOption,
                Rationale = rationale,
                Timestamp = DateTime.UtcNow
            };

            trace.Decisions.Add(decision);
        }

        _logger.LogDebug("Trace {TraceId} - {Agent} decided: {Decision} (from {Options})",
            traceId, agentName, selectedOption, string.Join(", ", options));
    }

    public void CompleteTrace(Guid traceId, bool success, string? error = null)
    {
        if (!_traces.TryGetValue(traceId, out var trace))
        {
            _logger.LogWarning("Attempted to complete non-existent trace {TraceId}", traceId);
            return;
        }

        trace.EndTime = DateTime.UtcNow;
        trace.IsComplete = true;
        trace.IsSuccess = success;
        trace.Error = error;

        if (success)
        {
            _logger.LogInformation(
                "Completed trace {TraceId} successfully in {Duration}ms with {Steps} steps",
                traceId, trace.Duration.TotalMilliseconds, trace.Steps.Count);
        }
        else
        {
            _logger.LogWarning(
                "Completed trace {TraceId} with error: {Error}",
                traceId, error);
        }
    }

    public ReasoningTrace? GetTrace(Guid traceId)
    {
        return _traces.TryGetValue(traceId, out var trace) ? trace : null;
    }

    public IReadOnlyList<ReasoningTrace> GetRecentTraces(int limit = 10)
    {
        return _traces.Values
            .Where(t => t.IsComplete)
            .OrderByDescending(t => t.EndTime ?? t.StartTime)
            .Take(limit)
            .ToList()
            .AsReadOnly();
    }

    public string FormatAsText(Guid traceId)
    {
        var trace = GetTrace(traceId);
        if (trace == null)
        {
            return $"Trace {traceId} not found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"REASONING TRACE: {trace.TraceId}");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Query: {trace.Query}");
        sb.AppendLine($"Request ID: {trace.RequestId}");
        sb.AppendLine($"Started: {trace.StartTime:yyyy-MM-dd HH:mm:ss.fff}");

        if (trace.IsComplete)
        {
            sb.AppendLine($"Ended: {trace.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Duration: {trace.Duration.TotalMilliseconds:F2}ms");
            sb.AppendLine($"Status: {(trace.IsSuccess ? "✓ Success" : $"✗ Failed: {trace.Error}")}");
        }
        else
        {
            sb.AppendLine("Status: In Progress...");
        }

        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine("REASONING STEPS:");
        sb.AppendLine("───────────────────────────────────────────────────────────────");

        foreach (var step in trace.Steps)
        {
            var elapsed = (step.Timestamp - trace.StartTime).TotalMilliseconds;
            sb.AppendLine($"  [{step.StepNumber}] +{elapsed:F0}ms | {step.AgentName}");
            sb.AppendLine($"      └─ {step.Description}");

            if (step.Metadata.Count > 0)
            {
                foreach (var (key, value) in step.Metadata)
                {
                    sb.AppendLine($"         • {key}: {value}");
                }
            }
        }

        if (trace.Decisions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("DECISIONS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");

            foreach (var decision in trace.Decisions)
            {
                sb.AppendLine($"  • {decision.AgentName} - {decision.DecisionType}");
                sb.AppendLine($"    Options: [{string.Join(", ", decision.Options)}]");
                sb.AppendLine($"    Selected: {decision.SelectedOption}");
                sb.AppendLine($"    Rationale: {decision.Rationale}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    public string FormatAsJson(Guid traceId)
    {
        var trace = GetTrace(traceId);
        if (trace == null)
        {
            return JsonSerializer.Serialize(new { error = $"Trace {traceId} not found." });
        }

        var jsonObject = new
        {
            traceId = trace.TraceId,
            requestId = trace.RequestId,
            query = trace.Query,
            startTime = trace.StartTime,
            endTime = trace.EndTime,
            durationMs = trace.Duration.TotalMilliseconds,
            isComplete = trace.IsComplete,
            isSuccess = trace.IsSuccess,
            error = trace.Error,
            steps = trace.Steps.Select(s => new
            {
                stepNumber = s.StepNumber,
                agentName = s.AgentName,
                description = s.Description,
                timestamp = s.Timestamp,
                elapsedMs = (s.Timestamp - trace.StartTime).TotalMilliseconds,
                metadata = s.Metadata
            }),
            decisions = trace.Decisions.Select(d => new
            {
                agentName = d.AgentName,
                decisionType = d.DecisionType,
                options = d.Options,
                selectedOption = d.SelectedOption,
                rationale = d.Rationale,
                timestamp = d.Timestamp
            })
        };

        return JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public void ClearOldTraces(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var oldTraceIds = _traces.Values
            .Where(t => t.IsComplete && (t.EndTime ?? t.StartTime) < cutoff)
            .Select(t => t.TraceId)
            .ToList();

        foreach (var traceId in oldTraceIds)
        {
            _traces.TryRemove(traceId, out _);
        }

        if (oldTraceIds.Count > 0)
        {
            _logger.LogDebug("Cleared {Count} old traces", oldTraceIds.Count);
        }
    }
}
