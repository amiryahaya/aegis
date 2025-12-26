using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class QueryHistoryRepository : IQueryHistoryRepository
{
    private readonly string _connectionString;

    public QueryHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<QueryHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, user_id, conversation_id, query, response,
                   tokens_used, response_time_ms, chunks_retrieved, retrieval_method,
                   metadata, created_at, updated_at
            FROM query_history
            WHERE id = @Id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<QueryHistoryRow>(sql, new { Id = id });

        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<QueryHistory>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, user_id, conversation_id, query, response,
                   tokens_used, response_time_ms, chunks_retrieved, retrieval_method,
                   metadata, created_at, updated_at
            FROM query_history
            WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC
            LIMIT @Limit
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var rows = await connection.QueryAsync<QueryHistoryRow>(sql, new { WorkspaceId = workspaceId, Limit = limit });

        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<QueryHistory>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, user_id, conversation_id, query, response,
                   tokens_used, response_time_ms, chunks_retrieved, retrieval_method,
                   metadata, created_at, updated_at
            FROM query_history
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT @Limit
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var rows = await connection.QueryAsync<QueryHistoryRow>(sql, new { UserId = userId, Limit = limit });

        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<QueryHistory>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, user_id, conversation_id, query, response,
                   tokens_used, response_time_ms, chunks_retrieved, retrieval_method,
                   metadata, created_at, updated_at
            FROM query_history
            WHERE conversation_id = @ConversationId
            ORDER BY created_at ASC
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var rows = await connection.QueryAsync<QueryHistoryRow>(sql, new { ConversationId = conversationId });

        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<QueryHistory>> SearchAsync(Guid workspaceId, string searchTerm, int limit = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, user_id, conversation_id, query, response,
                   tokens_used, response_time_ms, chunks_retrieved, retrieval_method,
                   metadata, created_at, updated_at
            FROM query_history
            WHERE workspace_id = @WorkspaceId
              AND to_tsvector('english', query || ' ' || response) @@ plainto_tsquery('english', @SearchTerm)
            ORDER BY created_at DESC
            LIMIT @Limit
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var rows = await connection.QueryAsync<QueryHistoryRow>(sql, new { WorkspaceId = workspaceId, SearchTerm = searchTerm, Limit = limit });

        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task AddAsync(QueryHistory queryHistory, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO query_history (id, workspace_id, user_id, conversation_id, query, response,
                                      tokens_used, response_time_ms, chunks_retrieved, retrieval_method,
                                      metadata, created_at, updated_at)
            VALUES (@Id, @WorkspaceId, @UserId, @ConversationId, @Query, @Response,
                    @TokensUsed, @ResponseTimeMs, @ChunksRetrieved, @RetrievalMethod,
                    @Metadata::jsonb, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, QueryHistoryRow.FromEntity(queryHistory));
    }

    public async Task<int> GetCountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM query_history WHERE workspace_id = @WorkspaceId";

        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new { WorkspaceId = workspaceId });
    }

    public async Task<QueryHistoryStats> GetStatsAsync(Guid workspaceId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT(*) as TotalQueries,
                COALESCE(SUM(tokens_used), 0) as TotalTokensUsed,
                COALESCE(AVG(response_time_ms), 0) as AverageResponseTimeMs,
                COALESCE(AVG(chunks_retrieved), 0) as AverageChunksRetrieved
            FROM query_history
            WHERE workspace_id = @WorkspaceId
              AND (@From IS NULL OR created_at >= @From)
              AND (@To IS NULL OR created_at <= @To)
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleAsync<QueryHistoryStats>(sql, new { WorkspaceId = workspaceId, From = from, To = to });
    }

    private record QueryHistoryRow(
        Guid Id,
        Guid Workspace_Id,
        Guid User_Id,
        Guid? Conversation_Id,
        string Query,
        string Response,
        int Tokens_Used,
        long Response_Time_Ms,
        int Chunks_Retrieved,
        string Retrieval_Method,
        string Metadata,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public QueryHistory ToEntity()
        {
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata) ?? new Dictionary<string, string>();

            return QueryHistory.Reconstitute(
                Id,
                Workspace_Id,
                User_Id,
                Conversation_Id,
                Query,
                Response,
                Tokens_Used,
                TimeSpan.FromMilliseconds(Response_Time_Ms),
                Chunks_Retrieved,
                Retrieval_Method,
                metadata,
                Created_At,
                Updated_At);
        }

        public static QueryHistoryRow FromEntity(QueryHistory entity)
        {
            var metadata = JsonSerializer.Serialize(entity.Metadata);

            return new QueryHistoryRow(
                entity.Id,
                entity.WorkspaceId,
                entity.UserId,
                entity.ConversationId,
                entity.Query,
                entity.Response,
                entity.TokensUsed,
                (long)entity.ResponseTime.TotalMilliseconds,
                entity.ChunksRetrieved,
                entity.RetrievalMethod,
                metadata,
                entity.CreatedAt,
                entity.UpdatedAt);
        }
    }
}
