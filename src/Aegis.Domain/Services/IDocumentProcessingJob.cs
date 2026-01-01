using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Job for processing documents asynchronously
/// </summary>
public interface IDocumentProcessingJob : IBackgroundJob
{
    /// <summary>
    /// Processes a document (parsing, chunking, embedding generation)
    /// </summary>
    /// <param name="documentId">The document to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<DocumentProcessingResult>> ProcessDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of document processing
/// </summary>
public record DocumentProcessingResult(
    Guid DocumentId,
    int ChunksCreated,
    int EmbeddingsGenerated,
    TimeSpan ProcessingTime,
    DocumentProcessingStatus Status,
    string? ErrorMessage = null);

/// <summary>
/// Status of document processing
/// </summary>
public enum DocumentProcessingStatus
{
    Pending,
    Parsing,
    Chunking,
    GeneratingEmbeddings,
    StoringVectors,
    Completed,
    Failed
}

/// <summary>
/// Job for generating embeddings for document chunks
/// </summary>
public interface IEmbeddingGenerationJob : IBackgroundJob
{
    /// <summary>
    /// Generates embeddings for a batch of document chunks
    /// </summary>
    /// <param name="documentId">The document whose chunks need embeddings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<EmbeddingGenerationResult>> GenerateEmbeddingsAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of embedding generation
/// </summary>
public record EmbeddingGenerationResult(
    Guid DocumentId,
    int ChunksProcessed,
    int EmbeddingsGenerated,
    TimeSpan ProcessingTime,
    string? ErrorMessage = null);

/// <summary>
/// Job for syncing data sources
/// </summary>
public interface IDataSourceSyncJob : IBackgroundJob
{
    /// <summary>
    /// Syncs a data source to fetch new or updated content
    /// </summary>
    /// <param name="dataSourceId">The data source to sync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<DataSourceSyncResult>> SyncDataSourceAsync(
        Guid dataSourceId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of data source sync
/// </summary>
public record DataSourceSyncResult(
    Guid DataSourceId,
    int DocumentsAdded,
    int DocumentsUpdated,
    int DocumentsRemoved,
    TimeSpan SyncTime,
    DateTime? NextSyncAt,
    string? ErrorMessage = null);

/// <summary>
/// Job for cleaning up expired cache entries and old data
/// </summary>
public interface ICleanupJob : IBackgroundJob
{
    /// <summary>
    /// Cleans up expired data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<CleanupResult>> CleanupAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Result of cleanup operation
/// </summary>
public record CleanupResult(
    int ExpiredCacheEntriesRemoved,
    int OldAuditLogsArchived,
    int OrphanedChunksRemoved,
    TimeSpan CleanupTime);
