using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class SyncHistory : AggregateRoot
{
    public Guid DataSourceId { get; private set; }
    public SyncType Type { get; private set; }
    public SyncStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int DocumentsAdded { get; private set; }
    public int DocumentsUpdated { get; private set; }
    public int DocumentsDeleted { get; private set; }
    public int DocumentsFailed { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();

    // For cursor-based incremental sync
    public string? LastSyncCursor { get; private set; }

    private SyncHistory() { } // For ORM

    public static SyncHistory Create(Guid dataSourceId, SyncType type)
    {
        return new SyncHistory
        {
            Id = UuidGenerator.NewId(),
            DataSourceId = dataSourceId,
            Type = type,
            Status = SyncStatus.Running,
            StartedAt = DateTime.UtcNow,
            DocumentsAdded = 0,
            DocumentsUpdated = 0,
            DocumentsDeleted = 0,
            DocumentsFailed = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Complete(int added, int updated, int deleted, int failed, string? cursor = null)
    {
        Status = SyncStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        DocumentsAdded = added;
        DocumentsUpdated = updated;
        DocumentsDeleted = deleted;
        DocumentsFailed = failed;
        LastSyncCursor = cursor;
        SetUpdated();
    }

    public void MarkFailed(string errorMessage)
    {
        Status = SyncStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        SetUpdated();
    }

    public void UpdateProgress(int added, int updated, int deleted, int failed)
    {
        DocumentsAdded = added;
        DocumentsUpdated = updated;
        DocumentsDeleted = deleted;
        DocumentsFailed = failed;
        SetUpdated();
    }

    public static SyncHistory Reconstitute(
        Guid id,
        Guid dataSourceId,
        SyncType type,
        SyncStatus status,
        DateTime startedAt,
        DateTime? completedAt,
        int documentsAdded,
        int documentsUpdated,
        int documentsDeleted,
        int documentsFailed,
        string? errorMessage,
        Dictionary<string, string> metadata,
        string? lastSyncCursor,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new SyncHistory
        {
            Id = id,
            DataSourceId = dataSourceId,
            Type = type,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DocumentsAdded = documentsAdded,
            DocumentsUpdated = documentsUpdated,
            DocumentsDeleted = documentsDeleted,
            DocumentsFailed = documentsFailed,
            ErrorMessage = errorMessage,
            Metadata = metadata,
            LastSyncCursor = lastSyncCursor,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

public enum SyncType
{
    Full,
    Incremental
}

public enum SyncStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

public static class SyncHistoryErrors
{
    public static readonly Error NotFound = Error.NotFound("SyncHistory.NotFound", "Sync history not found");
    public static readonly Error SyncInProgress = Error.Validation("SyncHistory.InProgress", "A sync is already in progress for this data source");
}
