using Aegis.Domain.Common;
using Aegis.Domain.Services;
using System.Security.Cryptography;
using System.Text;

namespace Aegis.Infrastructure.Services.Embedding;

/// <summary>
/// In-memory embedding service for testing purposes.
/// Generates deterministic embeddings based on text hashing.
/// For production use, replace with OnnxEmbeddingService.
/// </summary>
public class InMemoryEmbeddingService : IEmbeddingService
{
    private readonly int _dimension;

    public int EmbeddingDimension => _dimension;

    public InMemoryEmbeddingService(int dimension = 384)
    {
        _dimension = dimension;
    }

    public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(Result<float[]>.Failure(
                Error.Validation("Embedding.EmptyText", "Text cannot be empty")));
        }

        var embedding = GenerateDeterministicEmbedding(text);
        return Task.FromResult(Result<float[]>.Success(embedding));
    }

    public async Task<Result<List<float[]>>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();

        if (textList.Count == 0)
        {
            return Result<List<float[]>>.Success(new List<float[]>());
        }

        var embeddings = new List<float[]>();

        foreach (var text in textList)
        {
            var result = await GenerateEmbeddingAsync(text, cancellationToken);

            if (result.IsFailure)
            {
                return Result<List<float[]>>.Failure(result.Error!);
            }

            embeddings.Add(result.Value);
        }

        return Result<List<float[]>>.Success(embeddings);
    }

    private float[] GenerateDeterministicEmbedding(string text)
    {
        // Generate a deterministic embedding based on text hash
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        var embedding = new float[_dimension];
        var random = new Random(BitConverter.ToInt32(hash, 0));

        // Generate random values with normal distribution
        for (int i = 0; i < _dimension; i++)
        {
            // Box-Muller transform for normal distribution
            var u1 = random.NextDouble();
            var u2 = random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            embedding[i] = (float)randStdNormal;
        }

        // Normalize to unit vector
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < _dimension; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}
