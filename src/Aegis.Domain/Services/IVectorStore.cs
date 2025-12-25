using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Interface for vector storage and similarity search
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Creates a new collection in the vector store
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="vectorDimension">Dimension of the vectors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> CreateCollectionAsync(
        string collectionName,
        int vectorDimension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a collection exists
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<bool>> CollectionExistsAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection from the vector store
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> DeleteCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a single vector with metadata
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="point">The vector point to upsert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> UpsertAsync(
        string collectionName,
        VectorPoint point,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts multiple vectors with metadata in batch
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="points">The vector points to upsert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> UpsertBatchAsync(
        string collectionName,
        IEnumerable<VectorPoint> points,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for similar vectors
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="queryVector">The query vector</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="scoreThreshold">Minimum similarity score (0-1)</param>
    /// <param name="filter">Optional metadata filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<List<VectorSearchResult>>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int limit = 10,
        float? scoreThreshold = null,
        Dictionary<string, string>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes vectors by IDs
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="ids">The IDs of vectors to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> DeleteAsync(
        string collectionName,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a vector point with metadata
/// </summary>
public record VectorPoint
{
    /// <summary>
    /// Unique identifier for the vector
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The embedding vector
    /// </summary>
    public required float[] Vector { get; init; }

    /// <summary>
    /// Metadata associated with the vector
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Represents a vector search result
/// </summary>
public record VectorSearchResult
{
    /// <summary>
    /// The vector point ID
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Similarity score (0-1, higher is more similar)
    /// </summary>
    public required float Score { get; init; }

    /// <summary>
    /// The embedding vector
    /// </summary>
    public required float[] Vector { get; init; }

    /// <summary>
    /// Metadata associated with the vector
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
