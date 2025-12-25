using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class WorkspaceFactRepository : IWorkspaceFactRepository
{
    private readonly string _connectionString;

    public WorkspaceFactRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WorkspaceFact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, statement, confidence, source_document_ids,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_facts
            WHERE id = @Id
            """;

        var row = await connection.QuerySingleOrDefaultAsync<WorkspaceFactDto>(sql, new { Id = id });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<WorkspaceFact>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, statement, confidence, source_document_ids,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_facts
            WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC
            """;

        var rows = await connection.QueryAsync<WorkspaceFactDto>(sql, new { WorkspaceId = workspaceId });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<IEnumerable<WorkspaceFact>> GetByConfidenceAsync(Guid workspaceId, string confidence, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, workspace_id, statement, confidence, source_document_ids,
                   source_conversation_id, added_by, created_at, updated_at
            FROM workspace_facts
            WHERE workspace_id = @WorkspaceId AND confidence = @Confidence
            ORDER BY created_at DESC
            """;

        var rows = await connection.QueryAsync<WorkspaceFactDto>(sql, new { WorkspaceId = workspaceId, Confidence = confidence });
        return rows.Select(r => r.ToEntity());
    }

    public async Task<Guid> CreateAsync(WorkspaceFact fact, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO workspace_facts (id, workspace_id, statement, confidence, source_document_ids,
                                       source_conversation_id, added_by, created_at)
            VALUES (@Id, @WorkspaceId, @Statement, @Confidence, @SourceDocumentIds::jsonb,
                    @SourceConversationId, @AddedBy, @CreatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            fact.Id,
            fact.WorkspaceId,
            fact.Statement,
            fact.Confidence,
            SourceDocumentIds = JsonSerializer.Serialize(fact.SourceDocumentIds),
            fact.SourceConversationId,
            fact.AddedBy,
            fact.CreatedAt
        });

        return fact.Id;
    }

    public async Task UpdateAsync(WorkspaceFact fact, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE workspace_facts
            SET statement = @Statement,
                confidence = @Confidence,
                source_document_ids = @SourceDocumentIds::jsonb,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(sql, new
        {
            fact.Id,
            fact.Statement,
            fact.Confidence,
            SourceDocumentIds = JsonSerializer.Serialize(fact.SourceDocumentIds),
            fact.UpdatedAt
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM workspace_facts WHERE id = @Id", new { Id = id });
    }

    private class WorkspaceFactDto
    {
        public Guid id { get; set; }
        public Guid workspace_id { get; set; }
        public string statement { get; set; } = string.Empty;
        public string confidence { get; set; } = "Confirmed";
        public string source_document_ids { get; set; } = "[]";
        public Guid? source_conversation_id { get; set; }
        public Guid added_by { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }

        public WorkspaceFact ToEntity()
        {
            var sourceDocumentIds = JsonSerializer.Deserialize<List<Guid>>(source_document_ids) ?? new List<Guid>();
            return WorkspaceFact.Reconstitute(
                id, workspace_id, statement, confidence, sourceDocumentIds,
                source_conversation_id, added_by, created_at, updated_at);
        }
    }
}
