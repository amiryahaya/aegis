using System.ComponentModel;
using System.Text.Json;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Features.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin for BM25 keyword-based search
/// </summary>
public class KeywordSearchPlugin
{
    private readonly IKeywordSearchService _searchService;
    private readonly ILogger<KeywordSearchPlugin> _logger;

    public KeywordSearchPlugin(
        IKeywordSearchService searchService,
        ILogger<KeywordSearchPlugin> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [KernelFunction, Description("Search for documents using exact keyword matching and BM25 ranking algorithm")]
    public async Task<string> KeywordSearchAsync(
        [Description("The search query with specific keywords to match")] string query,
        [Description("The workspace ID to search within")] string workspaceId,
        [Description("Maximum number of results to return (default: 10)")] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { error = "Query cannot be empty" });
        }

        if (!Guid.TryParse(workspaceId, out var parsedWorkspaceId))
        {
            return JsonSerializer.Serialize(new { error = "Invalid workspace ID" });
        }

        _logger.LogInformation(
            "Keyword search: Query='{Query}', WorkspaceId={WorkspaceId}, TopK={TopK}",
            query, parsedWorkspaceId, topK);

        // Perform keyword search
        var result = await _searchService.SearchAsync(
            query,
            parsedWorkspaceId,
            topK,
            cancellationToken);

        // Handle result
        return result.Match(
            success =>
            {
                var results = success.Select(r => new
                {
                    documentId = r.DocumentId,
                    title = r.Title,
                    content = r.Content,
                    bm25Score = r.BM25Score,
                    matchedTerms = r.MatchedTerms,
                    metadata = r.Metadata
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = results.Count,
                    results
                }, new JsonSerializerOptions { WriteIndented = true });
            },
            error =>
            {
                _logger.LogWarning(
                    "Keyword search failed: {ErrorCode} - {ErrorMessage}",
                    error.Code, error.Message);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Search failed: {error.Message}"
                });
            });
    }
}
