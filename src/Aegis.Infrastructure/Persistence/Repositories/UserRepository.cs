using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, email, name, role, is_active, created_at, updated_at
            FROM users
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<UserRecord>(sql, new { Id = id });

        return record?.ToUser();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, email, name, role, is_active, created_at, updated_at
            FROM users
            WHERE email = @Email
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<UserRecord>(sql, new { Email = email.ToLowerInvariant() });

        return record?.ToUser();
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, email, name, role, is_active, created_at, updated_at
            FROM users
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<UserRecord>(sql);

        return records.Select(r => r.ToUser()).ToList();
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO users (id, email, name, role, is_active, created_at, updated_at)
            VALUES (@Id, @Email, @Name, @Role, @IsActive, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            user.Id,
            user.Email,
            user.Name,
            Role = user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            UpdatedAt = user.UpdatedAt ?? user.CreatedAt
        });
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE users
            SET name = @Name,
                role = @Role,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            user.Id,
            user.Name,
            Role = user.Role.ToString(),
            user.IsActive,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM users WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users WHERE id = @Id)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id });
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users WHERE email = @Email)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Email = email.ToLowerInvariant() });
    }

    private record UserRecord(
        Guid Id,
        string Email,
        string Name,
        string Role,
        bool Is_Active,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public User ToUser()
        {
            var role = Enum.Parse<UserRole>(Role, ignoreCase: true);
            return User.Reconstitute(Id, Email, Name, role, Is_Active, Created_At, Updated_At);
        }
    }
}
