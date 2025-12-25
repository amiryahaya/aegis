using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

/// <summary>
/// Curated entity in workspace knowledge base
/// </summary>
public class WorkspaceEntity : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty; // Person, Organization, Location, etc.
    public string? Description { get; private set; }
    public List<string> Aliases { get; private set; } = new();
    public string Confidence { get; private set; } = "Medium"; // Low, Medium, High, Confirmed
    public Guid? SourceConversationId { get; private set; }
    public Guid AddedBy { get; private set; }

    private WorkspaceEntity() { } // For ORM

    public static WorkspaceEntity Create(
        Guid workspaceId,
        string name,
        string type,
        Guid addedBy,
        string? description = null,
        List<string>? aliases = null,
        string confidence = "Medium",
        Guid? sourceConversationId = null)
    {
        return new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Type = type,
            Description = description,
            Aliases = aliases ?? new List<string>(),
            Confidence = confidence,
            SourceConversationId = sourceConversationId,
            AddedBy = addedBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static WorkspaceEntity Reconstitute(
        Guid id,
        Guid workspaceId,
        string name,
        string type,
        string? description,
        List<string> aliases,
        string confidence,
        Guid? sourceConversationId,
        Guid addedBy,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new WorkspaceEntity
        {
            Id = id,
            WorkspaceId = workspaceId,
            Name = name,
            Type = type,
            Description = description,
            Aliases = aliases,
            Confidence = confidence,
            SourceConversationId = sourceConversationId,
            AddedBy = addedBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateDetails(string name, string? description, List<string>? aliases = null)
    {
        Name = name;
        Description = description;
        if (aliases != null)
        {
            Aliases = aliases;
        }
        SetUpdated();
    }

    public void UpdateConfidence(string confidence)
    {
        Confidence = confidence;
        SetUpdated();
    }
}
