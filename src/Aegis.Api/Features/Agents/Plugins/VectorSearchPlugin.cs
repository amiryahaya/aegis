using System.ComponentModel;
using System.Text.Json;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Features.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin for vector-based semantic search
/// </summary>
public class VectorSearchPlugin
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<VectorSearchPlugin> _logger;

    public VectorSearchPlugin(
        ISemanticSearchService searchService,
        ILogger<VectorSearchPlugin> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [KernelFunction, Description("Search for relevant documents using semantic similarity based on meaning, not just keywords")]
    public async Task<string> VectorSearchAsync(
        [Description("The search query to find relevant documents")] string query,
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
            "Vector search: Query='{Query}', WorkspaceId={WorkspaceId}, TopK={TopK}",
            query, parsedWorkspaceId, topK);

        // Perform semantic search
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
                    score = r.Score,
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
                    "Vector search failed: {ErrorCode} - {ErrorMessage}",
                    error.Code, error.Message);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Search failed: {error.Message}"
                });
            });
    }
}
