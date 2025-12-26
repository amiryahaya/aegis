using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

public interface IQueryHistoryRepository
{
    Task<QueryHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueryHistory>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueryHistory>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueryHistory>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueryHistory>> SearchAsync(Guid workspaceId, string searchTerm, int limit = 50, CancellationToken cancellationToken = default);
    Task AddAsync(QueryHistory queryHistory, CancellationToken cancellationToken = default);
    Task<int> GetCountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<QueryHistoryStats> GetStatsAsync(Guid workspaceId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}

public record QueryHistoryStats(
    int TotalQueries,
    int TotalTokensUsed,
    double AverageResponseTimeMs,
    double AverageChunksRetrieved);
