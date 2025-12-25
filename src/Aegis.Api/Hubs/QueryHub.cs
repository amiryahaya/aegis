using System.Security.Claims;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Aegis.Api.Hubs;

[Authorize]
public class QueryHub : Hub
{
    private readonly ITeamRepository _teamRepository;
    private readonly IHybridRetriever _hybridRetriever;
    private readonly ILLMService _llmService;
    private readonly ILogger<QueryHub> _logger;

    public QueryHub(
        ITeamRepository teamRepository,
        IHybridRetriever hybridRetriever,
        ILLMService llmService,
        ILogger<QueryHub> logger)
    {
        _teamRepository = teamRepository;
        _hybridRetriever = hybridRetriever;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task StreamQuery(Guid teamId, string query, int maxResults = 5)
    {
        try
        {
            // Verify team exists
            var teamExists = await _teamRepository.ExistsAsync(teamId);
            if (!teamExists)
            {
                await Clients.Caller.SendAsync("Error", "Team not found");
                return;
            }

            // Validate query
            if (string.IsNullOrWhiteSpace(query))
            {
                await Clients.Caller.SendAsync("Error", "Query cannot be empty");
                return;
            }

            // Retrieve relevant chunks using hybrid search
            var collectionName = $"team_{teamId}";
            var retrievalResult = await _hybridRetriever.RetrieveAsync(
                collectionName,
                query,
                limit: maxResults);

            if (retrievalResult.IsFailure)
            {
                await Clients.Caller.SendAsync("Error", retrievalResult.Error!.Message);
                return;
            }

            var searchResults = retrievalResult.Value;

            // Send citations first
            var citations = searchResults.Select(r => new
            {
                DocumentId = Guid.Parse(r.Metadata.GetValueOrDefault("documentId", Guid.Empty.ToString())),
                Text = r.Text,
                Score = r.Score,
                ChunkIndex = int.Parse(r.Metadata.GetValueOrDefault("chunkIndex", "0"))
            }).ToList();

            await Clients.Caller.SendAsync("Citations", citations);

            // Extract context from search results
            var contexts = searchResults.Select(r => r.Text).ToList();

            // Build prompt for LLM
            var prompt = BuildRAGPrompt(query, contexts);

            // Stream response tokens
            await foreach (var tokenResult in _llmService.GenerateStreamingResponseAsync(prompt))
            {
                if (tokenResult.IsSuccess)
                {
                    await Clients.Caller.SendAsync("StreamToken", tokenResult.Value);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", tokenResult.Error!.Message);
                    return;
                }
            }

            // Signal completion
            await Clients.Caller.SendAsync("StreamComplete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming query response for team {TeamId}", teamId);
            await Clients.Caller.SendAsync("Error", "An error occurred while processing your query");
        }
    }

    private static string BuildRAGPrompt(string query, List<string> contexts)
    {
        var contextStr = string.Join("\n\n", contexts.Select((c, i) => $"[{i + 1}] {c}"));

        return $@"You are a helpful assistant. Answer the following question based on the provided context.

Context:
{contextStr}

Question: {query}

Answer:";
    }
}
