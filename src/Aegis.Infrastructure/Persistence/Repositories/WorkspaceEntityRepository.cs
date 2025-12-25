using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class WorkspaceEntityRepository : IWorkspaceEntityRepository
{
    private readonly string _connectionString;

    public WorkspaceEntityRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WorkspaceEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, name, type, description, aliases, confidence,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_entities
            WHERE id = @Id
            """;

        var row = await connection.QuerySingleOrDefaultAsync<WorkspaceEntityDto>(sql, new { Id = id });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<WorkspaceEntity>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, name, type, description, aliases, confidence,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_entities
            WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC
            """;

        var rows = await connection.QueryAsync<WorkspaceEntityDto>(sql, new { WorkspaceId = workspaceId });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<IEnumerable<WorkspaceEntity>> GetByTypeAsync(Guid workspaceId, string type, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, name, type, description, aliases, confidence,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_entities
            WHERE workspace_id = @WorkspaceId AND type = @Type
            ORDER BY created_at DESC
            """;

        var rows = await connection.QueryAsync<WorkspaceEntityDto>(sql, new { WorkspaceId = workspaceId, Type = type });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<WorkspaceEntity?> GetByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, name, type, description, aliases, confidence,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_entities
            WHERE workspace_id = @WorkspaceId AND name = @Name
            LIMIT 1
            """;

        var row = await connection.QuerySingleOrDefaultAsync<WorkspaceEntityDto>(sql, new { WorkspaceId = workspaceId, Name = name });
        return row?.ToEntity();
    }

    public async Task<Guid> CreateAsync(WorkspaceEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO workspace_entities (id, workspace_id, name, type, description, aliases, confidence,
                                          source_conversation_id, added_by, created_at)
            VALUES (@Id, @WorkspaceId, @Name, @Type, @Description, @Aliases::jsonb, @Confidence,
                    @SourceConversationId, @AddedBy, @CreatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.WorkspaceId,
            entity.Name,
            entity.Type,
            entity.Description,
            Aliases = JsonSerializer.Serialize(entity.Aliases),
            entity.Confidence,
            entity.SourceConversationId,
            entity.AddedBy,
            entity.CreatedAt
        });

        return entity.Id;
    }

    public async Task UpdateAsync(WorkspaceEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE workspace_entities
            SET name = @Name,
                description = @Description,
                aliases = @Aliases::jsonb,
                confidence = @Confidence,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Description,
            Aliases = JsonSerializer.Serialize(entity.Aliases),
            entity.Confidence,
            entity.UpdatedAt
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM workspace_entities WHERE id = @Id", new { Id = id });
    }

    private class WorkspaceEntityDto
    {
        public Guid id { get; set; }
        public Guid workspace_id { get; set; }
        public string name { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string? description { get; set; }
        public string aliases { get; set; } = "[]";
        public string confidence { get; set; } = "Medium";
        public Guid? source_conversation_id { get; set; }
        public Guid added_by { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }

        public WorkspaceEntity ToEntity()
        {
            var aliasesList = JsonSerializer.Deserialize<List<string>>(aliases) ?? new List<string>();
            return WorkspaceEntity.Reconstitute(
                id, workspace_id, name, type, description, aliasesList,
                confidence, source_conversation_id, added_by, created_at, updated_at);
        }
    }
}
