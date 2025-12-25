using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly string _connectionString;

    public WorkspaceRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, created_by, status, created_at, updated_at
            FROM workspaces
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<WorkspaceRecord>(sql, new { Id = id });

        return record?.ToWorkspace();
    }

    public async Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, created_by, status, created_at, updated_at
            FROM workspaces
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<WorkspaceRecord>(sql);

        return records.Select(r => r.ToWorkspace()).ToList();
    }

    public async Task<IReadOnlyList<Workspace>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, team_id, created_by, status, created_at, updated_at
            FROM workspaces
            WHERE team_id = @TeamId
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<WorkspaceRecord>(sql, new { TeamId = teamId });

        return records.Select(r => r.ToWorkspace()).ToList();
    }

    public async Task<IReadOnlyList<Workspace>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Get workspaces created by user or belonging to teams the user is a member of
        const string sql = """
            SELECT DISTINCT w.id, w.name, w.description, w.team_id, w.created_by, w.status, w.created_at, w.updated_at
            FROM workspaces w
            LEFT JOIN team_members tm ON w.team_id = tm.team_id
            LEFT JOIN teams t ON w.team_id = t.id
            WHERE w.created_by = @UserId OR tm.user_id = @UserId OR t.owner_id = @UserId
            ORDER BY w.created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<WorkspaceRecord>(sql, new { UserId = userId });

        return records.Select(r => r.ToWorkspace()).ToList();
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO workspaces (id, name, description, team_id, created_by, status, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @Description, @TeamId, @CreatedBy, @Status, @IsActive, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.TeamId,
            workspace.CreatedBy,
            Status = workspace.Status.ToString(),
            IsActive = workspace.Status == WorkspaceStatus.Active,
            workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt ?? workspace.CreatedAt
        });
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE workspaces
            SET name = @Name,
                description = @Description,
                team_id = @TeamId,
                status = @Status,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.TeamId,
            Status = workspace.Status.ToString(),
            IsActive = workspace.Status == WorkspaceStatus.Active,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM workspaces WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM workspaces WHERE id = @Id)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id });
    }

    public async Task<bool> NameExistsInTeamAsync(string name, Guid? teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM workspaces
                WHERE name = @Name
                AND (team_id = @TeamId OR (@TeamId IS NULL AND team_id IS NULL))
            )
            """;

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Name = name, TeamId = teamId });
    }

    private record WorkspaceRecord(
        Guid Id,
        string Name,
        string? Description,
        Guid? Team_Id,
        Guid? Created_By,
        string Status,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public Workspace ToWorkspace()
        {
            var status = Enum.Parse<WorkspaceStatus>(Status, ignoreCase: true);
            return Workspace.Reconstitute(Id, Name, Description, Team_Id, Created_By ?? Guid.Empty, status, Created_At, Updated_At);
        }
    }
}
