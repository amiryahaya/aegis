using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

/// <summary>
/// Established fact in workspace knowledge base
/// </summary>
public class WorkspaceFact : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Statement { get; private set; } = string.Empty;
    public string Confidence { get; private set; } = "Confirmed"; // Suspected, Likely, Confirmed, Verified
    public List<Guid> SourceDocumentIds { get; private set; } = new();
    public Guid? SourceConversationId { get; private set; }
    public Guid AddedBy { get; private set; }

    private WorkspaceFact() { } // For ORM

    public static WorkspaceFact Create(
        Guid workspaceId,
        string statement,
        Guid addedBy,
        string confidence = "Confirmed",
        List<Guid>? sourceDocumentIds = null,
        Guid? sourceConversationId = null)
    {
        return new WorkspaceFact
        {
            Id = UuidGenerator.NewId(),
            WorkspaceId = workspaceId,
            Statement = statement,
            Confidence = confidence,
            SourceDocumentIds = sourceDocumentIds ?? new List<Guid>(),
            SourceConversationId = sourceConversationId,
            AddedBy = addedBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static WorkspaceFact Reconstitute(
        Guid id,
        Guid workspaceId,
        string statement,
        string confidence,
        List<Guid> sourceDocumentIds,
        Guid? sourceConversationId,
        Guid addedBy,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new WorkspaceFact
        {
            Id = id,
            WorkspaceId = workspaceId,
            Statement = statement,
            Confidence = confidence,
            SourceDocumentIds = sourceDocumentIds,
            SourceConversationId = sourceConversationId,
            AddedBy = addedBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateStatement(string statement)
    {
        Statement = statement;
        SetUpdated();
    }

    public void UpdateConfidence(string confidence)
    {
        Confidence = confidence;
        SetUpdated();
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
