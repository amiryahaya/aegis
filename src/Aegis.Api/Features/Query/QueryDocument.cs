using Aegis.Domain.Common;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Query;

public static class QueryDocument
{
    public record Command : IRequest<Result<Response>>
    {
        public required Guid TeamId { get; init; }
        public required string Query { get; init; }
        public int MaxResults { get; init; } = 5;
        public float? ScoreThreshold { get; init; }
    }

    public record Response
    {
        public required string Answer { get; init; }
        public required List<Citation> Citations { get; init; }
        public required int ProcessingTimeMs { get; init; }
    }

    public record Citation
    {
        public required Guid DocumentId { get; init; }
        public required string Text { get; init; }
        public required float Score { get; init; }
        public required int ChunkIndex { get; init; }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IHybridRetriever _hybridRetriever;
        private readonly ILLMService _llmService;

        public Handler(
            ITeamRepository teamRepository,
            IHybridRetriever hybridRetriever,
            ILLMService llmService)
        {
            _teamRepository = teamRepository;
            _hybridRetriever = hybridRetriever;
            _llmService = llmService;
        }

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            // Verify team exists
            var teamExists = await _teamRepository.ExistsAsync(request.TeamId, cancellationToken);
            if (!teamExists)
            {
                return Result<Response>.Failure(
                    Error.NotFound("Query.TeamNotFound", "Team not found"));
            }

            // Validate query
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Result<Response>.Failure(
                    Error.Validation("Query.EmptyQuery", "Query cannot be empty"));
            }

            // Retrieve relevant chunks using hybrid search
            var collectionName = $"team_{request.TeamId}";
            var retrievalResult = await _hybridRetriever.RetrieveAsync(
                collectionName,
                request.Query,
                limit: request.MaxResults,
                scoreThreshold: request.ScoreThreshold,
                filter: null,
                cancellationToken: cancellationToken);

            if (retrievalResult.IsFailure)
            {
                return Result<Response>.Failure(retrievalResult.Error!);
            }

            var searchResults = retrievalResult.Value;

            // Extract context from search results
            var contexts = searchResults.Select(r => r.Text).ToList();

            // Generate answer using LLM
            var llmResult = await _llmService.GenerateRAGResponseAsync(
                request.Query,
                contexts,
                cancellationToken);

            if (llmResult.IsFailure)
            {
                return Result<Response>.Failure(llmResult.Error!);
            }

            var ragResponse = llmResult.Value;

            // Build citations from search results
            var citations = searchResults.Select(r => new Citation
            {
                DocumentId = Guid.Parse(r.Metadata.GetValueOrDefault("documentId", Guid.Empty.ToString())),
                Text = r.Text,
                Score = r.Score,
                ChunkIndex = int.Parse(r.Metadata.GetValueOrDefault("chunkIndex", "0"))
            }).ToList();

            var processingTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return Result<Response>.Success(new Response
            {
                Answer = ragResponse.Response,
                Citations = citations,
                ProcessingTimeMs = processingTime
            });
        }
    }
}
