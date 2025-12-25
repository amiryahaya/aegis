using Aegis.Domain.Common;
using Aegis.Domain.Services;
using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Services.BM25;

/// <summary>
/// In-memory BM25 indexer implementation for testing purposes.
/// For production use, replace with ElasticsearchBM25Indexer.
/// </summary>
public class InMemoryBM25Indexer : IBM25Indexer
{
    private readonly Dictionary<string, BM25Index> _indices = new();

    // BM25 parameters
    private const float K1 = 1.2f;
    private const float B = 0.75f;

    public Task<Result> CreateIndexAsync(
        string indexName,
        CancellationToken cancellationToken = default)
    {
        if (_indices.ContainsKey(indexName))
        {
            return Task.FromResult(Result.Failure(
                Error.Conflict("BM25.IndexExists", $"Index '{indexName}' already exists")));
        }

        _indices[indexName] = new BM25Index();
        return Task.FromResult(Result.Success());
    }

    public Task<Result<bool>> IndexExistsAsync(
        string indexName,
        CancellationToken cancellationToken = default)
    {
        var exists = _indices.ContainsKey(indexName);
        return Task.FromResult(Result<bool>.Success(exists));
    }

    public Task<Result> DeleteIndexAsync(
        string indexName,
        CancellationToken cancellationToken = default)
    {
        if (!_indices.ContainsKey(indexName))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("BM25.IndexNotFound", $"Index '{indexName}' not found")));
        }

        _indices.Remove(indexName);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> IndexDocumentAsync(
        string indexName,
        BM25Document document,
        CancellationToken cancellationToken = default)
    {
        if (!_indices.TryGetValue(indexName, out var index))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("BM25.IndexNotFound", $"Index '{indexName}' not found")));
        }

        var tokens = Tokenize(document.Text);
        var termFrequencies = CalculateTermFrequencies(tokens);

        index.Documents[document.Id] = new IndexedDocument
        {
            Id = document.Id,
            Text = document.Text,
            Metadata = new Dictionary<string, string>(document.Metadata),
            Length = tokens.Count,
            TermFrequencies = termFrequencies
        };

        // Update document frequencies
        foreach (var term in termFrequencies.Keys)
        {
            if (!index.DocumentFrequencies.ContainsKey(term))
            {
                index.DocumentFrequencies[term] = 0;
            }
            index.DocumentFrequencies[term]++;
        }

        return Task.FromResult(Result.Success());
    }

    public async Task<Result> IndexBatchAsync(
        string indexName,
        IEnumerable<BM25Document> documents,
        CancellationToken cancellationToken = default)
    {
        foreach (var document in documents)
        {
            var result = await IndexDocumentAsync(indexName, document, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    public Task<Result<List<BM25SearchResult>>> SearchAsync(
        string indexName,
        string query,
        int limit = 10,
        float? scoreThreshold = null,
        Dictionary<string, string>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (!_indices.TryGetValue(indexName, out var index))
        {
            return Task.FromResult(Result<List<BM25SearchResult>>.Failure(
                Error.NotFound("BM25.IndexNotFound", $"Index '{indexName}' not found")));
        }

        if (index.Documents.Count == 0)
        {
            return Task.FromResult(Result<List<BM25SearchResult>>.Success(new List<BM25SearchResult>()));
        }

        var queryTokens = Tokenize(query);
        var results = new List<BM25SearchResult>();

        // Calculate average document length
        var avgDocLength = (float)index.Documents.Values.Average(d => d.Length);

        foreach (var doc in index.Documents.Values)
        {
            // Apply metadata filter if provided
            if (filter != null && filter.Count > 0)
            {
                var matchesFilter = filter.All(kvp =>
                    doc.Metadata.TryGetValue(kvp.Key, out var value) && value == kvp.Value);

                if (!matchesFilter)
                {
                    continue;
                }
            }

            // Calculate BM25 score
            var score = CalculateBM25Score(queryTokens, doc, index, avgDocLength);

            // Skip documents with zero score (no matching terms)
            if (score <= 0)
            {
                continue;
            }

            // Apply score threshold if provided
            if (scoreThreshold.HasValue && score < scoreThreshold.Value)
            {
                continue;
            }

            results.Add(new BM25SearchResult
            {
                Id = doc.Id,
                Score = score,
                Text = doc.Text,
                Metadata = new Dictionary<string, string>(doc.Metadata)
            });
        }

        // Sort by score descending and take top results
        var topResults = results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();

        return Task.FromResult(Result<List<BM25SearchResult>>.Success(topResults));
    }

    public Task<Result> DeleteAsync(
        string indexName,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (!_indices.TryGetValue(indexName, out var index))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("BM25.IndexNotFound", $"Index '{indexName}' not found")));
        }

        foreach (var id in ids)
        {
            if (index.Documents.TryGetValue(id, out var doc))
            {
                // Update document frequencies
                foreach (var term in doc.TermFrequencies.Keys)
                {
                    if (index.DocumentFrequencies.ContainsKey(term))
                    {
                        index.DocumentFrequencies[term]--;
                        if (index.DocumentFrequencies[term] <= 0)
                        {
                            index.DocumentFrequencies.Remove(term);
                        }
                    }
                }

                index.Documents.Remove(id);
            }
        }

        return Task.FromResult(Result.Success());
    }

    private static List<string> Tokenize(string text)
    {
        // Simple tokenization: lowercase, split on non-alphanumeric, remove empty
        var normalized = text.ToLowerInvariant();
        var tokens = Regex.Split(normalized, @"\W+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return tokens;
    }

    private static Dictionary<string, int> CalculateTermFrequencies(List<string> tokens)
    {
        var frequencies = new Dictionary<string, int>();

        foreach (var token in tokens)
        {
            if (!frequencies.ContainsKey(token))
            {
                frequencies[token] = 0;
            }
            frequencies[token]++;
        }

        return frequencies;
    }

    private static float CalculateBM25Score(
        List<string> queryTokens,
        IndexedDocument document,
        BM25Index index,
        float avgDocLength)
    {
        float score = 0;
        var totalDocs = index.Documents.Count;

        foreach (var term in queryTokens)
        {
            if (!document.TermFrequencies.TryGetValue(term, out var termFreq))
            {
                continue; // Term not in document
            }

            // Calculate IDF (Inverse Document Frequency)
            var docsWithTerm = index.DocumentFrequencies.GetValueOrDefault(term, 0);
            if (docsWithTerm == 0) continue;

            var idf = MathF.Log((totalDocs - docsWithTerm + 0.5f) / (docsWithTerm + 0.5f) + 1.0f);

            // Calculate BM25 component for this term
            var numerator = termFreq * (K1 + 1);
            var denominator = termFreq + K1 * (1 - B + B * document.Length / avgDocLength);

            score += idf * (numerator / denominator);
        }

        return score;
    }

    private class BM25Index
    {
        public Dictionary<Guid, IndexedDocument> Documents { get; } = new();
        public Dictionary<string, int> DocumentFrequencies { get; } = new();
    }

    private class IndexedDocument
    {
        public required Guid Id { get; init; }
        public required string Text { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();
        public required int Length { get; init; }
        public required Dictionary<string, int> TermFrequencies { get; init; }
    }
}
