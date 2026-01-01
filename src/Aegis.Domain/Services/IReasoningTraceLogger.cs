namespace Aegis.Domain.Services;

/// <summary>
/// Service for logging and tracking reasoning steps across agents
/// </summary>
public interface IReasoningTraceLogger
{
    /// <summary>
    /// Starts a new reasoning trace for a request
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="query">The original query</param>
    /// <returns>Trace ID for tracking</returns>
    Guid StartTrace(Guid requestId, string query);

    /// <summary>
    /// Adds a reasoning step to a trace
    /// </summary>
    /// <param name="traceId">The trace ID</param>
    /// <param name="agentName">Name of the agent performing the step</param>
    /// <param name="description">Description of the step</param>
    /// <param name="metadata">Optional metadata for the step</param>
    void AddStep(Guid traceId, string agentName, string description,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Records a decision point in the trace
    /// </summary>
    /// <param name="traceId">The trace ID</param>
    /// <param name="agentName">Name of the agent making the decision</param>
    /// <param name="decisionType">Type of decision</param>
    /// <param name="options">Available options</param>
    /// <param name="selectedOption">The selected option</param>
    /// <param name="rationale">Rationale for the decision</param>
    void AddDecision(Guid traceId, string agentName, string decisionType,
        string[] options, string selectedOption, string rationale);

    /// <summary>
    /// Marks a trace as complete
    /// </summary>
    /// <param name="traceId">The trace ID</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="error">Error message if not successful</param>
    void CompleteTrace(Guid traceId, bool success, string? error = null);

    /// <summary>
    /// Gets a trace by ID
    /// </summary>
    /// <param name="traceId">The trace ID</param>
    /// <returns>The trace or null if not found</returns>
    ReasoningTrace? GetTrace(Guid traceId);

    /// <summary>
    /// Gets recent traces
    /// </summary>
    /// <param name="limit">Maximum number of traces to return</param>
    /// <returns>List of recent traces</returns>
    IReadOnlyList<ReasoningTrace> GetRecentTraces(int limit = 10);

    /// <summary>
    /// Formats a trace as human-readable text
    /// </summary>
    /// <param name="traceId">The trace ID</param>
    /// <returns>Formatted text representation</returns>
    string FormatAsText(Guid traceId);

    /// <summary>
    /// Formats a trace as JSON
    /// </summary>
    /// <param name="traceId">The trace ID</param>
    /// <returns>JSON representation</returns>
    string FormatAsJson(Guid traceId);

    /// <summary>
    /// Clears traces older than the specified age
    /// </summary>
    /// <param name="maxAge">Maximum age of traces to keep</param>
    void ClearOldTraces(TimeSpan maxAge);
}

/// <summary>
/// Represents a complete reasoning trace
/// </summary>
public record ReasoningTrace
{
    /// <summary>
    /// Unique identifier for this trace
    /// </summary>
    public Guid TraceId { get; init; }

    /// <summary>
    /// The original request ID
    /// </summary>
    public Guid RequestId { get; init; }

    /// <summary>
    /// The original query
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// When the trace started
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// When the trace ended (null if not complete)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total duration of the trace
    /// </summary>
    public TimeSpan Duration => EndTime.HasValue
        ? EndTime.Value - StartTime
        : DateTime.UtcNow - StartTime;

    /// <summary>
    /// Whether the trace is complete
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if not successful
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Reasoning steps in order
    /// </summary>
    public List<ReasoningStep> Steps { get; init; } = new();

    /// <summary>
    /// Decision points recorded
    /// </summary>
    public List<DecisionPoint> Decisions { get; init; } = new();
}

/// <summary>
/// A single reasoning step in the trace
/// </summary>
public record ReasoningStep
{
    /// <summary>
    /// Step number (1-based)
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// Name of the agent performing the step
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Description of what was done
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// When the step occurred
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// A decision point in the reasoning process
/// </summary>
public record DecisionPoint
{
    /// <summary>
    /// Name of the agent making the decision
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Type of decision (e.g., "Route selection", "Tool choice")
    /// </summary>
    public required string DecisionType { get; init; }

    /// <summary>
    /// Available options that were considered
    /// </summary>
    public string[] Options { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The option that was selected
    /// </summary>
    public required string SelectedOption { get; init; }

    /// <summary>
    /// Rationale for the decision
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// When the decision was made
    /// </summary>
    public DateTime Timestamp { get; init; }
}
