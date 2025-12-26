using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

/// <summary>
/// Feedback on RAG query responses
/// </summary>
public class Feedback : AggregateRoot
{
    public Guid QueryHistoryId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public FeedbackType Type { get; private set; }
    public string? Comment { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();

    private Feedback() { } // For ORM

    public static Feedback Create(
        Guid queryHistoryId,
        Guid userId,
        Guid workspaceId,
        FeedbackType type,
        string? comment = null,
        Dictionary<string, string>? metadata = null)
    {
        return new Feedback
        {
            Id = UuidGenerator.NewId(),
            QueryHistoryId = queryHistoryId,
            UserId = userId,
            WorkspaceId = workspaceId,
            Type = type,
            Comment = comment,
            Metadata = metadata ?? new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(FeedbackType type, string? comment = null)
    {
        Type = type;
        Comment = comment;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddMetadata(string key, string value)
    {
        Metadata[key] = value;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Type of feedback
/// </summary>
public enum FeedbackType
{
    Positive = 1,
    Negative = 2,
    Neutral = 3
}
