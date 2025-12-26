using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Base interface for all agents in the system
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique identifier for this agent type
    /// </summary>
    string AgentType { get; }

    /// <summary>
    /// Execute the agent's task
    /// </summary>
    Task<Result<AgentResponse>> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to an agent
/// </summary>
public record AgentRequest
{
    /// <summary>
    /// Unique request ID for tracking
    /// </summary>
    public required Guid RequestId { get; init; }

    /// <summary>
    /// The task or query for the agent
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Context from previous agents or conversation
    /// </summary>
    public Dictionary<string, object> Context { get; init; } = new();

    /// <summary>
    /// User ID making the request
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Workspace ID for scoping
    /// </summary>
    public Guid? WorkspaceId { get; init; }
}

/// <summary>
/// Response from an agent
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// The agent's result
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Reasoning trace for transparency
    /// </summary>
    public List<string> ReasoningSteps { get; init; } = new();
}
