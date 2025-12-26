using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly string _connectionString;

    public SyncHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SyncHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, data_source_id, type, status, started_at, completed_at,
                   documents_added, documents_updated, documents_deleted, documents_failed,
                   error_message, metadata, last_sync_cursor, created_at, updated_at
            FROM sync_history
            WHERE id = @Id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<SyncHistoryRow>(sql, new { Id = id });

        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<SyncHistory>> GetByDataSourceIdAsync(Guid dataSourceId, int limit = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, data_source_id, type, status, started_at, completed_at,
                   documents_added, documents_updated, documents_deleted, documents_failed,
                   error_message, metadata, last_sync_cursor, created_at, updated_at
            FROM sync_history
            WHERE data_source_id = @DataSourceId
            ORDER BY started_at DESC
            LIMIT @Limit
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var rows = await connection.QueryAsync<SyncHistoryRow>(sql, new { DataSourceId = dataSourceId, Limit = limit });

        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<SyncHistory?> GetLastSuccessfulSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, data_source_id, type, status, started_at, completed_at,
                   documents_added, documents_updated, documents_deleted, documents_failed,
                   error_message, metadata, last_sync_cursor, created_at, updated_at
            FROM sync_history
            WHERE data_source_id = @DataSourceId
              AND status = 'Completed'
            ORDER BY completed_at DESC
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<SyncHistoryRow>(sql, new { DataSourceId = dataSourceId });

        return row?.ToEntity();
    }

    public async Task<SyncHistory?> GetRunningSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, data_source_id, type, status, started_at, completed_at,
                   documents_added, documents_updated, documents_deleted, documents_failed,
                   error_message, metadata, last_sync_cursor, created_at, updated_at
            FROM sync_history
            WHERE data_source_id = @DataSourceId
              AND status = 'Running'
            ORDER BY started_at DESC
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<SyncHistoryRow>(sql, new { DataSourceId = dataSourceId });

        return row?.ToEntity();
    }

    public async Task AddAsync(SyncHistory syncHistory, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO sync_history (id, data_source_id, type, status, started_at, completed_at,
                                     documents_added, documents_updated, documents_deleted, documents_failed,
                                     error_message, metadata, last_sync_cursor, created_at, updated_at)
            VALUES (@Id, @DataSourceId, @Type, @Status, @StartedAt, @CompletedAt,
                    @DocumentsAdded, @DocumentsUpdated, @DocumentsDeleted, @DocumentsFailed,
                    @ErrorMessage, @Metadata::jsonb, @LastSyncCursor, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, SyncHistoryRow.FromEntity(syncHistory));
    }

    public async Task UpdateAsync(SyncHistory syncHistory, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sync_history
            SET type = @Type,
                status = @Status,
                started_at = @StartedAt,
                completed_at = @CompletedAt,
                documents_added = @DocumentsAdded,
                documents_updated = @DocumentsUpdated,
                documents_deleted = @DocumentsDeleted,
                documents_failed = @DocumentsFailed,
                error_message = @ErrorMessage,
                metadata = @Metadata::jsonb,
                last_sync_cursor = @LastSyncCursor,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, SyncHistoryRow.FromEntity(syncHistory));
    }

    public async Task<bool> HasRunningSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM sync_history
                WHERE data_source_id = @DataSourceId
                  AND status = 'Running'
            )
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<bool>(sql, new { DataSourceId = dataSourceId });
    }

    private class SyncHistoryRow
    {
        public Guid Id { get; init; }
        public Guid Data_Source_Id { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime Started_At { get; init; }
        public DateTime? Completed_At { get; init; }
        public int Documents_Added { get; init; }
        public int Documents_Updated { get; init; }
        public int Documents_Deleted { get; init; }
        public int Documents_Failed { get; init; }
        public string? Error_Message { get; init; }
        public string Metadata { get; init; } = "{}";
        public string? Last_Sync_Cursor { get; init; }
        public DateTime Created_At { get; init; }
        public DateTime Updated_At { get; init; }

        public SyncHistory ToEntity()
        {
            var syncType = Enum.Parse<SyncType>(Type);
            var syncStatus = Enum.Parse<SyncStatus>(Status);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata) ?? new();

            return SyncHistory.Reconstitute(
                Id,
                Data_Source_Id,
                syncType,
                syncStatus,
                Started_At,
                Completed_At,
                Documents_Added,
                Documents_Updated,
                Documents_Deleted,
                Documents_Failed,
                Error_Message,
                metadata,
                Last_Sync_Cursor,
                Created_At,
                Updated_At);
        }

        public static SyncHistoryRow FromEntity(SyncHistory entity)
        {
            var metadata = System.Text.Json.JsonSerializer.Serialize(entity.Metadata);

            return new SyncHistoryRow
            {
                Id = entity.Id,
                Data_Source_Id = entity.DataSourceId,
                Type = entity.Type.ToString(),
                Status = entity.Status.ToString(),
                Started_At = entity.StartedAt,
                Completed_At = entity.CompletedAt,
                Documents_Added = entity.DocumentsAdded,
                Documents_Updated = entity.DocumentsUpdated,
                Documents_Deleted = entity.DocumentsDeleted,
                Documents_Failed = entity.DocumentsFailed,
                Error_Message = entity.ErrorMessage,
                Metadata = metadata,
                Last_Sync_Cursor = entity.LastSyncCursor,
                Created_At = entity.CreatedAt,
                Updated_At = entity.UpdatedAt ?? entity.CreatedAt
            };
        }
    }
}
