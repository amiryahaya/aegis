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
            SELECT id, email, name, password_hash, role, is_active, email_verified, last_login_at,
                   failed_login_attempts, lockout_until, created_at, updated_at
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
            SELECT id, email, name, password_hash, role, is_active, email_verified, last_login_at,
                   failed_login_attempts, lockout_until, created_at, updated_at
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
            SELECT id, email, name, password_hash, role, is_active, email_verified, last_login_at,
                   failed_login_attempts, lockout_until, created_at, updated_at
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
            INSERT INTO users (id, email, name, password_hash, role, is_active, email_verified,
                             last_login_at, failed_login_attempts, lockout_until, created_at, updated_at)
            VALUES (@Id, @Email, @Name, @PasswordHash, @Role, @IsActive, @EmailVerified,
                    @LastLoginAt, @FailedLoginAttempts, @LockoutUntil, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            user.Id,
            user.Email,
            user.Name,
            user.PasswordHash,
            Role = user.Role.ToString(),
            user.IsActive,
            user.EmailVerified,
            user.LastLoginAt,
            user.FailedLoginAttempts,
            user.LockoutUntil,
            user.CreatedAt,
            UpdatedAt = user.UpdatedAt ?? user.CreatedAt
        });
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE users
            SET name = @Name,
                password_hash = @PasswordHash,
                role = @Role,
                is_active = @IsActive,
                email_verified = @EmailVerified,
                last_login_at = @LastLoginAt,
                failed_login_attempts = @FailedLoginAttempts,
                lockout_until = @LockoutUntil,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            user.Id,
            user.Name,
            user.PasswordHash,
            Role = user.Role.ToString(),
            user.IsActive,
            user.EmailVerified,
            user.LastLoginAt,
            user.FailedLoginAttempts,
            user.LockoutUntil,
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
        string? Password_Hash,
        string Role,
        bool Is_Active,
        bool Email_Verified,
        DateTime? Last_Login_At,
        int Failed_Login_Attempts,
        DateTime? Lockout_Until,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public User ToUser()
        {
            var role = Enum.Parse<UserRole>(Role, ignoreCase: true);
            return User.ReconstituteWithAuth(
                Id,
                Email,
                Name,
                Password_Hash,
                role,
                Is_Active,
                Email_Verified,
                Last_Login_At,
                Failed_Login_Attempts,
                Lockout_Until,
                Created_At,
                Updated_At);
        }
    }
}
