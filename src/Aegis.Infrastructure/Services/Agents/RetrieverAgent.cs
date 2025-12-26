using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Agent responsible for retrieving information
/// </summary>
public class RetrieverAgent : IAgent
{
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly IKeywordSearchService _keywordSearchService;
    private readonly ILogger<RetrieverAgent> _logger;

    public string AgentType => "Retriever";

    public RetrieverAgent(
        ISemanticSearchService semanticSearchService,
        IKeywordSearchService keywordSearchService,
        ILogger<RetrieverAgent> logger)
    {
        _semanticSearchService = semanticSearchService;
        _keywordSearchService = keywordSearchService;
        _logger = logger;
    }

    public async Task<Result<AgentResponse>> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retriever agent executing: {Task}", request.Task);

            var workspaceId = request.WorkspaceId ?? Guid.Empty;

            // Perform semantic search
            var searchResult = await _semanticSearchService.SearchAsync(
                request.Task,
                workspaceId,
                10,
                cancellationToken);

            if (searchResult.IsFailure)
            {
                return Result<AgentResponse>.Failure(searchResult.Error!);
            }

            // Format results as JSON
            var results = JsonSerializer.Serialize(searchResult.Value, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var response = new AgentResponse
            {
                Result = results,
                Confidence = 0.8,
                Metadata = new Dictionary<string, object>
                {
                    { "resultCount", searchResult.Value.Count },
                    { "searchType", "semantic" }
                },
                ReasoningSteps = new List<string>
                {
                    "Performed semantic search",
                    $"Retrieved {searchResult.Value.Count} results",
                    "Formatted results as JSON"
                }
            };

            return Result<AgentResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Retriever agent");
            return Result<AgentResponse>.Failure(
                Error.Internal("Retriever.Error", ex.Message));
        }
    }
}
