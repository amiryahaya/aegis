using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

/// <summary>
/// Key finding in workspace knowledge base
/// </summary>
public class WorkspaceFinding : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string Type { get; private set; } = "Evidence"; // Evidence, Hypothesis, Conclusion, Pattern
    public List<Guid> SupportingEntityIds { get; private set; } = new();
    public List<Guid> SourceDocumentIds { get; private set; } = new();
    public Guid? SourceConversationId { get; private set; }
    public Guid AddedBy { get; private set; }

    private WorkspaceFinding() { } // For ORM

    public static WorkspaceFinding Create(
        Guid workspaceId,
        string title,
        string content,
        Guid addedBy,
        string type = "Evidence",
        List<Guid>? supportingEntityIds = null,
        List<Guid>? sourceDocumentIds = null,
        Guid? sourceConversationId = null)
    {
        return new WorkspaceFinding
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = title,
            Content = content,
            Type = type,
            SupportingEntityIds = supportingEntityIds ?? new List<Guid>(),
            SourceDocumentIds = sourceDocumentIds ?? new List<Guid>(),
            SourceConversationId = sourceConversationId,
            AddedBy = addedBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static WorkspaceFinding Reconstitute(
        Guid id,
        Guid workspaceId,
        string title,
        string content,
        string type,
        List<Guid> supportingEntityIds,
        List<Guid> sourceDocumentIds,
        Guid? sourceConversationId,
        Guid addedBy,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new WorkspaceFinding
        {
            Id = id,
            WorkspaceId = workspaceId,
            Title = title,
            Content = content,
            Type = type,
            SupportingEntityIds = supportingEntityIds,
            SourceDocumentIds = sourceDocumentIds,
            SourceConversationId = sourceConversationId,
            AddedBy = addedBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateContent(string title, string content)
    {
        Title = title;
        Content = content;
        SetUpdated();
    }

    public void AddSupportingEntity(Guid entityId)
    {
        if (!SupportingEntityIds.Contains(entityId))
        {
            SupportingEntityIds.Add(entityId);
            SetUpdated();
        }
    }

    public void AddSourceDocument(Guid documentId)
    {
        if (!SourceDocumentIds.Contains(documentId))
        {
            SourceDocumentIds.Add(documentId);
            SetUpdated();
        }
    }
}
