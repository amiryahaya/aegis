using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class Conversation : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public ConversationStatus Status { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }

    private Conversation() { } // For ORM

    public static Conversation Create(
        Guid workspaceId,
        string title,
        Guid createdBy)
    {
        return new Conversation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = title,
            CreatedBy = createdBy,
            Status = ConversationStatus.Active,
            MessageCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes a Conversation from persistence. Use only in repositories.
    /// </summary>
    public static Conversation Reconstitute(
        Guid id,
        Guid workspaceId,
        string title,
        Guid createdBy,
        ConversationStatus status,
        DateTime? lastMessageAt,
        int messageCount,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new Conversation
        {
            Id = id,
            WorkspaceId = workspaceId,
            Title = title,
            CreatedBy = createdBy,
            Status = status,
            LastMessageAt = lastMessageAt,
            MessageCount = messageCount,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateTitle(string title)
    {
        Title = title;
        SetUpdated();
    }

    public void RecordMessage()
    {
        MessageCount++;
        LastMessageAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Archive()
    {
        Status = ConversationStatus.Archived;
        SetUpdated();
    }

    public void Activate()
    {
        Status = ConversationStatus.Active;
        SetUpdated();
    }

    public void Delete()
    {
        Status = ConversationStatus.Deleted;
        SetUpdated();
    }
}

public enum ConversationStatus
{
    Active,
    Archived,
    Deleted
}

public static class ConversationErrors
{
    public static readonly Error NotFound = Error.NotFound("Conversation.NotFound", "Conversation not found");
    public static readonly Error Archived = Error.Validation("Conversation.Archived", "Cannot modify an archived conversation");
    public static readonly Error Deleted = Error.Validation("Conversation.Deleted", "Cannot access a deleted conversation");
    public static readonly Error WorkspaceNotFound = Error.NotFound("Conversation.WorkspaceNotFound", "Workspace not found");
}
