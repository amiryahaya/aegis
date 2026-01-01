using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class DataSourceRepository : IDataSourceRepository
{
    private readonly string _connectionString;

    public DataSourceRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, workspace_id, type, status, created_by,
                   settings, document_count, total_size_bytes, last_indexed_at, created_at, updated_at
            FROM data_sources
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<DataSourceRecord>(sql, new { Id = id });

        return record?.ToDataSource();
    }

    public async Task<IReadOnlyList<DataSource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, workspace_id, type, status, created_by,
                   settings, document_count, total_size_bytes, last_indexed_at, created_at, updated_at
            FROM data_sources
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DataSourceRecord>(sql);

        return records.Select(r => r.ToDataSource()).ToList();
    }

    public async Task<IReadOnlyList<DataSource>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, workspace_id, type, status, created_by,
                   settings, document_count, total_size_bytes, last_indexed_at, created_at, updated_at
            FROM data_sources
            WHERE team_id = @TeamId
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DataSourceRecord>(sql, new { TeamId = teamId });

        return records.Select(r => r.ToDataSource()).ToList();
    }

    public async Task<IReadOnlyList<DataSource>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, workspace_id, type, status, created_by,
                   settings, document_count, total_size_bytes, last_indexed_at, created_at, updated_at
            FROM data_sources
            WHERE workspace_id = @WorkspaceId
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DataSourceRecord>(sql, new { WorkspaceId = workspaceId });

        return records.Select(r => r.ToDataSource()).ToList();
    }

    public async Task AddAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO data_sources (id, name, description, team_id, workspace_id, type, status, created_by,
                                     settings, document_count, total_size_bytes, last_indexed_at, created_at, updated_at)
            VALUES (@Id, @Name, @Description, @TeamId, @WorkspaceId, @Type, @Status, @CreatedBy,
                    @Settings::jsonb, @DocumentCount, @TotalSizeBytes, @LastIndexedAt, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.TeamId,
            dataSource.WorkspaceId,
            Type = dataSource.Type.ToString(),
            Status = dataSource.Status.ToString(),
            dataSource.CreatedBy,
            Settings = JsonSerializer.Serialize(dataSource.Settings),
            dataSource.DocumentCount,
            dataSource.TotalSizeBytes,
            dataSource.LastIndexedAt,
            dataSource.CreatedAt,
            UpdatedAt = dataSource.UpdatedAt ?? dataSource.CreatedAt
        });
    }

    public async Task UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE data_sources
            SET name = @Name,
                description = @Description,
                workspace_id = @WorkspaceId,
                type = @Type,
                status = @Status,
                settings = @Settings::jsonb,
                document_count = @DocumentCount,
                total_size_bytes = @TotalSizeBytes,
                last_indexed_at = @LastIndexedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.WorkspaceId,
            Type = dataSource.Type.ToString(),
            Status = dataSource.Status.ToString(),
            Settings = JsonSerializer.Serialize(dataSource.Settings),
            dataSource.DocumentCount,
            dataSource.TotalSizeBytes,
            dataSource.LastIndexedAt,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM data_sources WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM data_sources WHERE id = @Id)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id });
    }

    public async Task<bool> NameExistsInTeamAsync(string name, Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM data_sources WHERE name = @Name AND team_id = @TeamId)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Name = name, TeamId = teamId });
    }

    public async Task<IReadOnlyList<DataSource>> GetDueSyncAsync(CancellationToken cancellationToken = default)
    {
        // Get data sources that are due for sync based on status and last indexed time
        // For now, return active data sources that haven't been indexed in the last hour
        const string sql = """
            SELECT id, name, description, team_id, workspace_id, type, status, created_by,
                   settings, document_count, total_size_bytes, last_indexed_at, created_at, updated_at
            FROM data_sources
            WHERE status = 'Active'
              AND (last_indexed_at IS NULL OR last_indexed_at < @DueTime)
            ORDER BY last_indexed_at ASC NULLS FIRST
            LIMIT 10
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DataSourceRecord>(sql, new { DueTime = DateTime.UtcNow.AddHours(-1) });

        return records.Select(r => r.ToDataSource()).ToList();
    }

    private record DataSourceRecord(
        Guid Id,
        string Name,
        string? Description,
        Guid Team_Id,
        Guid? Workspace_Id,
        string Type,
        string Status,
        Guid Created_By,
        string Settings,
        int Document_Count,
        long Total_Size_Bytes,
        DateTime? Last_Indexed_At,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public DataSource ToDataSource()
        {
            var type = Enum.Parse<DataSourceType>(Type, ignoreCase: true);
            var status = Enum.Parse<DataSourceStatus>(Status, ignoreCase: true);
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings) ?? new Dictionary<string, string>();

            return DataSource.Reconstitute(
                Id,
                Name,
                Description,
                Team_Id,
                Workspace_Id,
                type,
                status,
                Created_By,
                settings,
                Document_Count,
                Total_Size_Bytes,
                Last_Indexed_At,
                Created_At,
                Updated_At);
        }
    }
}
