using System.Data;
using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class FeedbackRepository : IFeedbackRepository
{
    private readonly string _connectionString;

    public FeedbackRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Feedback?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, query_history_id, user_id, workspace_id, type, comment,
                   metadata, created_at, updated_at
            FROM feedback
            WHERE id = @Id";

        var row = await connection.QuerySingleOrDefaultAsync<FeedbackRow>(sql, new { Id = id });
        return row?.ToEntity();
    }

    public async Task<Feedback?> GetByQueryHistoryIdAsync(Guid queryHistoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, query_history_id, user_id, workspace_id, type, comment,
                   metadata, created_at, updated_at
            FROM feedback
            WHERE query_history_id = @QueryHistoryId";

        var row = await connection.QuerySingleOrDefaultAsync<FeedbackRow>(sql, new { QueryHistoryId = queryHistoryId });
        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<Feedback>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, query_history_id, user_id, workspace_id, type, comment,
                   metadata, created_at, updated_at
            FROM feedback
            WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC
            LIMIT @Limit";

        var rows = await connection.QueryAsync<FeedbackRow>(sql, new { WorkspaceId = workspaceId, Limit = limit });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<Feedback>> GetByUserIdAsync(
        Guid userId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, query_history_id, user_id, workspace_id, type, comment,
                   metadata, created_at, updated_at
            FROM feedback
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT @Limit";

        var rows = await connection.QueryAsync<FeedbackRow>(sql, new { UserId = userId, Limit = limit });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<FeedbackStats> GetStatsAsync(
        Guid workspaceId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                COUNT(*) as total_feedback,
                COUNT(*) FILTER (WHERE type = 1) as positive_feedback,
                COUNT(*) FILTER (WHERE type = 2) as negative_feedback,
                COUNT(*) FILTER (WHERE type = 3) as neutral_feedback,
                COUNT(*) FILTER (WHERE comment IS NOT NULL AND comment != '') as total_with_comments
            FROM feedback
            WHERE workspace_id = @WorkspaceId
                AND (@From IS NULL OR created_at >= @From)
                AND (@To IS NULL OR created_at <= @To)";

        var stats = await connection.QuerySingleAsync<dynamic>(sql, new
        {
            WorkspaceId = workspaceId,
            From = from,
            To = to
        });

        int totalFeedback = stats.total_feedback;
        int positiveFeedback = stats.positive_feedback;
        int negativeFeedback = stats.negative_feedback;
        int neutralFeedback = stats.neutral_feedback;
        int totalWithComments = stats.total_with_comments;

        double positiveRate = totalFeedback > 0
            ? Math.Round((double)positiveFeedback / totalFeedback * 100, 2)
            : 0;

        return new FeedbackStats(
            totalFeedback,
            positiveFeedback,
            negativeFeedback,
            neutralFeedback,
            positiveRate,
            totalWithComments);
    }

    public async Task AddAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO feedback (id, query_history_id, user_id, workspace_id, type, comment, metadata, created_at)
            VALUES (@Id, @QueryHistoryId, @UserId, @WorkspaceId, @Type, @Comment, @Metadata::jsonb, @CreatedAt)
            ON CONFLICT (query_history_id, user_id)
            DO UPDATE SET
                type = EXCLUDED.type,
                comment = EXCLUDED.comment,
                metadata = EXCLUDED.metadata,
                updated_at = NOW()";

        await connection.ExecuteAsync(sql, new
        {
            feedback.Id,
            feedback.QueryHistoryId,
            feedback.UserId,
            feedback.WorkspaceId,
            Type = (int)feedback.Type,
            feedback.Comment,
            Metadata = JsonSerializer.Serialize(feedback.Metadata),
            feedback.CreatedAt
        });
    }

    public async Task UpdateAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE feedback
            SET type = @Type,
                comment = @Comment,
                metadata = @Metadata::jsonb,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            feedback.Id,
            Type = (int)feedback.Type,
            feedback.Comment,
            Metadata = JsonSerializer.Serialize(feedback.Metadata),
            UpdatedAt = feedback.UpdatedAt ?? DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM feedback WHERE id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    private class FeedbackRow
    {
        public Guid Id { get; set; }
        public Guid Query_History_Id { get; set; }
        public Guid User_Id { get; set; }
        public Guid Workspace_Id { get; set; }
        public int Type { get; set; }
        public string? Comment { get; set; }
        public string Metadata { get; set; } = "{}";
        public DateTime Created_At { get; set; }
        public DateTime? Updated_At { get; set; }

        public Feedback ToEntity()
        {
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata)
                ?? new Dictionary<string, string>();

            var feedback = Feedback.Create(
                Query_History_Id,
                User_Id,
                Workspace_Id,
                (FeedbackType)Type,
                Comment,
                metadata);

            // Use reflection to set the protected Id and timestamps
            typeof(Feedback).GetProperty(nameof(Feedback.Id))!.SetValue(feedback, Id);
            typeof(Feedback).GetProperty(nameof(Feedback.CreatedAt))!.SetValue(feedback, Created_At);
            typeof(Feedback).GetProperty(nameof(Feedback.UpdatedAt))!.SetValue(feedback, Updated_At);

            return feedback;
        }
    }
}
