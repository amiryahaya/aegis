using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly string _connectionString;

    public TeamRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, owner_id as created_by, is_active, created_at, updated_at
            FROM teams
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<TeamRecord>(sql, new { Id = id });

        return record?.ToTeam();
    }

    public async Task<Team?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string teamSql = """
            SELECT id, name, description, owner_id as created_by, is_active, created_at, updated_at
            FROM teams
            WHERE id = @Id
            """;

        const string membersSql = """
            SELECT team_id, user_id, role, joined_at
            FROM team_members
            WHERE team_id = @TeamId
            """;

        await using var connection = CreateConnection();
        var teamRecord = await connection.QuerySingleOrDefaultAsync<TeamRecord>(teamSql, new { Id = id });

        if (teamRecord is null)
            return null;

        var memberRecords = await connection.QueryAsync<TeamMemberRecord>(membersSql, new { TeamId = id });
        var members = memberRecords.Select(m => m.ToTeamMember()).ToList();

        return teamRecord.ToTeam(members);
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, description, owner_id as created_by, is_active, created_at, updated_at
            FROM teams
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<TeamRecord>(sql);

        return records.Select(r => r.ToTeam()).ToList();
    }

    public async Task<IReadOnlyList<Team>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT t.id, t.name, t.description, t.owner_id as created_by, t.is_active, t.created_at, t.updated_at
            FROM teams t
            INNER JOIN team_members tm ON t.id = tm.team_id
            WHERE tm.user_id = @UserId
            ORDER BY t.created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<TeamRecord>(sql, new { UserId = userId });

        return records.Select(r => r.ToTeam()).ToList();
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO teams (id, name, description, owner_id, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @Description, @OwnerId, @IsActive, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            team.Id,
            team.Name,
            team.Description,
            OwnerId = team.CreatedBy,
            team.IsActive,
            team.CreatedAt,
            UpdatedAt = team.UpdatedAt ?? team.CreatedAt
        });
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE teams
            SET name = @Name,
                description = @Description,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            team.Id,
            team.Name,
            team.Description,
            team.IsActive,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM teams WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM teams WHERE id = @Id)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id });
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM teams WHERE name = @Name)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Name = name });
    }

    public async Task AddMemberAsync(Guid teamId, TeamMember member, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO team_members (team_id, user_id, role, joined_at)
            VALUES (@TeamId, @UserId, @Role, @JoinedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            TeamId = teamId,
            member.UserId,
            Role = member.Role.ToString(),
            member.JoinedAt
        });
    }

    public async Task RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM team_members WHERE team_id = @TeamId AND user_id = @UserId";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { TeamId = teamId, UserId = userId });
    }

    public async Task UpdateMemberRoleAsync(Guid teamId, Guid userId, TeamRole role, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE team_members
            SET role = @Role
            WHERE team_id = @TeamId AND user_id = @UserId
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            TeamId = teamId,
            UserId = userId,
            Role = role.ToString()
        });
    }

    private record TeamRecord(
        Guid Id,
        string Name,
        string? Description,
        Guid Created_By,
        bool Is_Active,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public Team ToTeam(IEnumerable<TeamMember>? members = null)
        {
            return Team.Reconstitute(Id, Name, Description, Created_By, Is_Active, Created_At, Updated_At, members);
        }
    }

    private record TeamMemberRecord(
        Guid Team_Id,
        Guid User_Id,
        string Role,
        DateTime Joined_At)
    {
        public TeamMember ToTeamMember()
        {
            var role = Enum.Parse<TeamRole>(Role, ignoreCase: true);
            return TeamMember.Reconstitute(Team_Id, User_Id, role, Joined_At);
        }
    }
}
