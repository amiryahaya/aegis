using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class QueryHistory : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public string Response { get; private set; } = string.Empty;
    public int TokensUsed { get; private set; }
    public TimeSpan ResponseTime { get; private set; }
    public int ChunksRetrieved { get; private set; }
    public string RetrievalMethod { get; private set; } = string.Empty; // hybrid, reranked, etc.
    public Dictionary<string, string> Metadata { get; private set; } = new();

    private QueryHistory() { } // For ORM

    public static QueryHistory Create(
        Guid workspaceId,
        Guid userId,
        string query,
        string response,
        int tokensUsed,
        TimeSpan responseTime,
        int chunksRetrieved,
        string retrievalMethod,
        Guid? conversationId = null,
        Dictionary<string, string>? metadata = null)
    {
        return new QueryHistory
        {
            Id = UuidGenerator.NewId(),
            WorkspaceId = workspaceId,
            UserId = userId,
            ConversationId = conversationId,
            Query = query,
            Response = response,
            TokensUsed = tokensUsed,
            ResponseTime = responseTime,
            ChunksRetrieved = chunksRetrieved,
            RetrievalMethod = retrievalMethod,
            Metadata = metadata ?? new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static QueryHistory Reconstitute(
        Guid id,
        Guid workspaceId,
        Guid userId,
        Guid? conversationId,
        string query,
        string response,
        int tokensUsed,
        TimeSpan responseTime,
        int chunksRetrieved,
        string retrievalMethod,
        Dictionary<string, string> metadata,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new QueryHistory
        {
            Id = id,
            WorkspaceId = workspaceId,
            UserId = userId,
            ConversationId = conversationId,
            Query = query,
            Response = response,
            TokensUsed = tokensUsed,
            ResponseTime = responseTime,
            ChunksRetrieved = chunksRetrieved,
            RetrievalMethod = retrievalMethod,
            Metadata = metadata,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

public static class QueryHistoryErrors
{
    public static readonly Error NotFound = Error.NotFound("QueryHistory.NotFound", "Query history not found");
}
