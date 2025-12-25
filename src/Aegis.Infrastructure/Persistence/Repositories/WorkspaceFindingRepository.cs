using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class WorkspaceFindingRepository : IWorkspaceFindingRepository
{
    private readonly string _connectionString;

    public WorkspaceFindingRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WorkspaceFinding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, title, content, type, supporting_entity_ids,
                   source_document_ids, source_conversation_id, added_by, created_at, updated_at
            FROM workspace_findings
            WHERE id = @Id
            """;

        var row = await connection.QuerySingleOrDefaultAsync<WorkspaceFindingDto>(sql, new { Id = id });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<WorkspaceFinding>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, title, content, type, supporting_entity_ids,
                   source_document_ids, source_conversation_id, added_by, created_at, updated_at
            FROM workspace_findings
            WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC
            """;

        var rows = await connection.QueryAsync<WorkspaceFindingDto>(sql, new { WorkspaceId = workspaceId });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<IEnumerable<WorkspaceFinding>> GetByTypeAsync(Guid workspaceId, string type, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, title, content, type, supporting_entity_ids,
                   source_document_ids, source_conversation_id, added_by, created_at, updated_at
            FROM workspace_findings
            WHERE workspace_id = @WorkspaceId AND type = @Type
            ORDER BY created_at DESC
            """;

        var rows = await connection.QueryAsync<WorkspaceFindingDto>(sql, new { WorkspaceId = workspaceId, Type = type });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<Guid> CreateAsync(WorkspaceFinding finding, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO workspace_findings (id, workspace_id, title, content, type, supporting_entity_ids,
                                          source_document_ids, source_conversation_id, added_by, created_at)
            VALUES (@Id, @WorkspaceId, @Title, @Content, @Type, @SupportingEntityIds::jsonb,
                    @SourceDocumentIds::jsonb, @SourceConversationId, @AddedBy, @CreatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            finding.Id,
            finding.WorkspaceId,
            finding.Title,
            finding.Content,
            finding.Type,
            SupportingEntityIds = JsonSerializer.Serialize(finding.SupportingEntityIds),
            SourceDocumentIds = JsonSerializer.Serialize(finding.SourceDocumentIds),
            finding.SourceConversationId,
            finding.AddedBy,
            finding.CreatedAt
        });

        return finding.Id;
    }

    public async Task UpdateAsync(WorkspaceFinding finding, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE workspace_findings
            SET title = @Title,
                content = @Content,
                type = @Type,
                supporting_entity_ids = @SupportingEntityIds::jsonb,
                source_document_ids = @SourceDocumentIds::jsonb,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(sql, new
        {
            finding.Id,
            finding.Title,
            finding.Content,
            finding.Type,
            SupportingEntityIds = JsonSerializer.Serialize(finding.SupportingEntityIds),
            SourceDocumentIds = JsonSerializer.Serialize(finding.SourceDocumentIds),
            finding.UpdatedAt
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM workspace_findings WHERE id = @Id", new { Id = id });
    }

    private class WorkspaceFindingDto
    {
        public Guid id { get; set; }
        public Guid workspace_id { get; set; }
        public string title { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public string type { get; set; } = "Evidence";
        public string supporting_entity_ids { get; set; } = "[]";
        public string source_document_ids { get; set; } = "[]";
        public Guid? source_conversation_id { get; set; }
        public Guid added_by { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }

        public WorkspaceFinding ToEntity()
        {
            var supportingEntityIds = JsonSerializer.Deserialize<List<Guid>>(supporting_entity_ids) ?? new List<Guid>();
            var sourceDocumentIds = JsonSerializer.Deserialize<List<Guid>>(source_document_ids) ?? new List<Guid>();
            return WorkspaceFinding.Reconstitute(
                id, workspace_id, title, content, type, supportingEntityIds,
                sourceDocumentIds, source_conversation_id, added_by, created_at, updated_at);
        }
    }
}
