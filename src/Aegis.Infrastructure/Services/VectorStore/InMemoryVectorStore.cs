using Aegis.Domain.Common;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.VectorStore;

/// <summary>
/// In-memory vector store implementation for testing purposes.
/// For production use, replace with QdrantVectorStore.
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<string, VectorCollection> _collections = new();

    public Task<Result> CreateCollectionAsync(
        string collectionName,
        int vectorDimension,
        CancellationToken cancellationToken = default)
    {
        if (_collections.ContainsKey(collectionName))
        {
            return Task.FromResult(Result.Failure(
                Error.Conflict("VectorStore.CollectionExists", $"Collection '{collectionName}' already exists")));
        }

        _collections[collectionName] = new VectorCollection(vectorDimension);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<bool>> CollectionExistsAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        var exists = _collections.ContainsKey(collectionName);
        return Task.FromResult(Result<bool>.Success(exists));
    }

    public Task<Result> DeleteCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        if (!_collections.ContainsKey(collectionName))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("VectorStore.CollectionNotFound", $"Collection '{collectionName}' not found")));
        }

        _collections.Remove(collectionName);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> UpsertAsync(
        string collectionName,
        VectorPoint point,
        CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collectionName, out var collection))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("VectorStore.CollectionNotFound", $"Collection '{collectionName}' not found")));
        }

        if (point.Vector.Length != collection.Dimension)
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("VectorStore.DimensionMismatch",
                    $"Vector dimension {point.Vector.Length} does not match collection dimension {collection.Dimension}")));
        }

        collection.Points[point.Id] = point;
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> UpsertBatchAsync(
        string collectionName,
        IEnumerable<VectorPoint> points,
        CancellationToken cancellationToken = default)
    {
        foreach (var point in points)
        {
            var result = await UpsertAsync(collectionName, point, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    public Task<Result<List<VectorSearchResult>>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int limit = 10,
        float? scoreThreshold = null,
        Dictionary<string, string>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collectionName, out var collection))
        {
            return Task.FromResult(Result<List<VectorSearchResult>>.Failure(
                Error.NotFound("VectorStore.CollectionNotFound", $"Collection '{collectionName}' not found")));
        }

        if (queryVector.Length != collection.Dimension)
        {
            return Task.FromResult(Result<List<VectorSearchResult>>.Failure(
                Error.Validation("VectorStore.DimensionMismatch",
                    $"Query vector dimension {queryVector.Length} does not match collection dimension {collection.Dimension}")));
        }

        var results = new List<VectorSearchResult>();

        foreach (var point in collection.Points.Values)
        {
            // Apply metadata filter if provided
            if (filter != null && filter.Count > 0)
            {
                var matchesFilter = filter.All(kvp =>
                    point.Metadata.TryGetValue(kvp.Key, out var value) && value == kvp.Value);

                if (!matchesFilter)
                {
                    continue;
                }
            }

            // Calculate cosine similarity
            var score = CosineSimilarity(queryVector, point.Vector);

            // Apply score threshold if provided
            if (scoreThreshold.HasValue && score < scoreThreshold.Value)
            {
                continue;
            }

            results.Add(new VectorSearchResult
            {
                Id = point.Id,
                Score = score,
                Vector = point.Vector,
                Metadata = new Dictionary<string, string>(point.Metadata)
            });
        }

        // Sort by score descending and take top results
        var topResults = results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();

        return Task.FromResult(Result<List<VectorSearchResult>>.Success(topResults));
    }

    public Task<Result> DeleteAsync(
        string collectionName,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collectionName, out var collection))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("VectorStore.CollectionNotFound", $"Collection '{collectionName}' not found")));
        }

        foreach (var id in ids)
        {
            collection.Points.Remove(id);
        }

        return Task.FromResult(Result.Success());
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    private class VectorCollection
    {
        public int Dimension { get; }
        public Dictionary<Guid, VectorPoint> Points { get; } = new();

        public VectorCollection(int dimension)
        {
            Dimension = dimension;
        }
    }
}
