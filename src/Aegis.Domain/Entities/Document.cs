using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class Document : AggregateRoot
{
    public string FileName { get; private set; } = string.Empty;
    public string? Title { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string? StoragePath { get; private set; }
    public string? Content { get; private set; } // For inline content from connectors
    public string? ExternalId { get; private set; } // For tracking external source records
    public Guid DataSourceId { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DocumentStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int ChunkCount { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();

    private Document() { } // For ORM

    public static Document Create(
        string fileName,
        string contentType,
        long sizeBytes,
        Guid dataSourceId,
        Guid uploadedBy,
        string? title = null,
        string? storagePath = null,
        string? content = null,
        string? externalId = null)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            Title = title ?? fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StoragePath = storagePath,
            Content = content,
            ExternalId = externalId,
            DataSourceId = dataSourceId,
            UploadedBy = uploadedBy,
            Status = DocumentStatus.Pending,
            ChunkCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes a Document from persistence. Use only in repositories.
    /// </summary>
    public static Document Reconstitute(
        Guid id,
        string fileName,
        string? title,
        string contentType,
        long sizeBytes,
        string? storagePath,
        string? content,
        string? externalId,
        Guid dataSourceId,
        Guid uploadedBy,
        DocumentStatus status,
        string? errorMessage,
        int chunkCount,
        DateTime? processedAt,
        Dictionary<string, string> metadata,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new Document
        {
            Id = id,
            FileName = fileName,
            Title = title,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StoragePath = storagePath,
            Content = content,
            ExternalId = externalId,
            DataSourceId = dataSourceId,
            UploadedBy = uploadedBy,
            Status = status,
            ErrorMessage = errorMessage,
            ChunkCount = chunkCount,
            ProcessedAt = processedAt,
            Metadata = metadata,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void MarkProcessing()
    {
        Status = DocumentStatus.Processing;
        ErrorMessage = null;
        SetUpdated();
    }

    public void MarkProcessed(int chunkCount)
    {
        Status = DocumentStatus.Processed;
        ChunkCount = chunkCount;
        ProcessedAt = DateTime.UtcNow;
        ErrorMessage = null;
        SetUpdated();
    }

    public void MarkFailed(string errorMessage)
    {
        Status = DocumentStatus.Failed;
        ErrorMessage = errorMessage;
        SetUpdated();
    }

    public void UpdateMetadata(Dictionary<string, string> metadata)
    {
        Metadata = metadata;
        SetUpdated();
    }

    public void UpdateTitle(string title)
    {
        Title = title;
        SetUpdated();
    }

    public void UpdateContent(string content, long sizeBytes)
    {
        Content = content;
        SizeBytes = sizeBytes;
        SetUpdated();
    }
}

public enum DocumentStatus
{
    Pending,
    Processing,
    Processed,
    Failed
}

public static class DocumentErrors
{
    public static readonly Error NotFound = Error.NotFound("Document.NotFound", "Document not found");
    public static readonly Error InvalidFileType = Error.Validation("Document.InvalidFileType", "Unsupported file type");
    public static readonly Error FileTooLarge = Error.Validation("Document.FileTooLarge", "File size exceeds maximum allowed size");
    public static readonly Error ProcessingFailed = Error.Internal("Document.ProcessingFailed", "Document processing failed");
}
