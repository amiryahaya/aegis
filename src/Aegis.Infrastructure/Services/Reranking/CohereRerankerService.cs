using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Reranking;

/// <summary>
/// Reranker service using Cohere's rerank API
/// </summary>
public class CohereRerankerService : IRerankerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CohereRerankerService> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public CohereRerankerService(
        IHttpClientFactory httpClientFactory,
        ILogger<CohereRerankerService> logger,
        string apiKey,
        string model = "rerank-english-v3.0")
    {
        _httpClient = httpClientFactory.CreateClient("CohereReranker");
        _httpClient.BaseAddress = new Uri("https://api.cohere.ai/v1/");
        _logger = logger;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<Result<IReadOnlyList<RankedDocument>>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<IReadOnlyList<RankedDocument>>.Failure(
                Error.Validation("Reranker.EmptyQuery", "Query cannot be empty"));
        }

        if (documents == null || documents.Count == 0)
        {
            return Result<IReadOnlyList<RankedDocument>>.Success(Array.Empty<RankedDocument>());
        }

        try
        {
            var request = new CohereRerankRequest
            {
                Query = query,
                Documents = documents.ToList(),
                TopN = Math.Min(topK, documents.Count),
                Model = _model,
                ReturnDocuments = false
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "rerank")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CohereRerankResponse>(cancellationToken: cancellationToken);
            if (result?.Results == null)
            {
                return Result<IReadOnlyList<RankedDocument>>.Failure(
                    Error.Internal("Reranker.InvalidResponse", "Invalid response from reranking service"));
            }

            var rankedDocs = result.Results
                .Select(r => new RankedDocument(
                    Index: r.Index,
                    Content: documents[r.Index],
                    RelevanceScore: r.RelevanceScore))
                .ToList();

            _logger.LogInformation("Reranked {Count} documents, returned top {TopK}", documents.Count, rankedDocs.Count);

            return Result<IReadOnlyList<RankedDocument>>.Success(rankedDocs);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during reranking");
            return Result<IReadOnlyList<RankedDocument>>.Failure(
                Error.Internal("Reranker.HttpError", $"Reranking service error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reranking");
            return Result<IReadOnlyList<RankedDocument>>.Failure(
                Error.Internal("Reranker.Error", ex.Message));
        }
    }

    private class CohereRerankRequest
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("documents")]
        public List<string> Documents { get; set; } = new();

        [JsonPropertyName("top_n")]
        public int TopN { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("return_documents")]
        public bool ReturnDocuments { get; set; }
    }

    private class CohereRerankResponse
    {
        [JsonPropertyName("results")]
        public List<RerankResult> Results { get; set; } = new();
    }

    private class RerankResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("relevance_score")]
        public double RelevanceScore { get; set; }
    }
}
