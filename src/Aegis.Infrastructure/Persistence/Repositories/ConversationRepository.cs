using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly string _connectionString;

    public ConversationRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, title, created_by, status, last_message_at, message_count, created_at, updated_at
            FROM conversations
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<ConversationRecord>(sql, new { Id = id });

        return record?.ToConversation();
    }

    public async Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, title, created_by, status, last_message_at, message_count, created_at, updated_at
            FROM conversations
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<ConversationRecord>(sql);

        return records.Select(r => r.ToConversation()).ToList();
    }

    public async Task<IReadOnlyList<Conversation>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, title, created_by, status, last_message_at, message_count, created_at, updated_at
            FROM conversations
            WHERE workspace_id = @WorkspaceId
            ORDER BY last_message_at DESC NULLS LAST, created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<ConversationRecord>(sql, new { WorkspaceId = workspaceId });

        return records.Select(r => r.ToConversation()).ToList();
    }

    public async Task<IReadOnlyList<Conversation>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT c.id, c.workspace_id, c.title, c.created_by, c.status, c.last_message_at, c.message_count, c.created_at, c.updated_at
            FROM conversations c
            INNER JOIN workspaces w ON c.workspace_id = w.id
            LEFT JOIN team_members tm ON w.team_id = tm.team_id
            LEFT JOIN teams t ON w.team_id = t.id
            WHERE c.created_by = @UserId OR w.created_by = @UserId OR tm.user_id = @UserId OR t.owner_id = @UserId
            ORDER BY c.last_message_at DESC NULLS LAST, c.created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<ConversationRecord>(sql, new { UserId = userId });

        return records.Select(r => r.ToConversation()).ToList();
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO conversations (id, workspace_id, title, created_by, status, last_message_at, message_count, is_active, created_at, updated_at)
            VALUES (@Id, @WorkspaceId, @Title, @CreatedBy, @Status, @LastMessageAt, @MessageCount, @IsActive, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            conversation.Id,
            conversation.WorkspaceId,
            conversation.Title,
            conversation.CreatedBy,
            Status = conversation.Status.ToString(),
            conversation.LastMessageAt,
            conversation.MessageCount,
            IsActive = conversation.Status == ConversationStatus.Active,
            conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt ?? conversation.CreatedAt
        });
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE conversations
            SET title = @Title,
                status = @Status,
                last_message_at = @LastMessageAt,
                message_count = @MessageCount,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            conversation.Id,
            conversation.Title,
            Status = conversation.Status.ToString(),
            conversation.LastMessageAt,
            conversation.MessageCount,
            IsActive = conversation.Status == ConversationStatus.Active,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM conversations WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM conversations WHERE id = @Id)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id });
    }

    public async Task<int> CountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM conversations WHERE workspace_id = @WorkspaceId";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { WorkspaceId = workspaceId });
    }

    private record ConversationRecord(
        Guid Id,
        Guid Workspace_Id,
        string Title,
        Guid Created_By,
        string Status,
        DateTime? Last_Message_At,
        int Message_Count,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public Conversation ToConversation()
        {
            var status = Enum.Parse<ConversationStatus>(Status, ignoreCase: true);
            return Conversation.Reconstitute(
                Id,
                Workspace_Id,
                Title,
                Created_By,
                status,
                Last_Message_At,
                Message_Count,
                Created_At,
                Updated_At);
        }
    }
}
