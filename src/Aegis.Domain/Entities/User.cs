using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class User : AggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public List<string> Permissions { get; private set; } = [];
    public bool IsActive { get; private set; }
    public bool EmailVerified { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutUntil { get; private set; }

    private User() { } // For ORM

    public static User Create(string email, string name, UserRole role = UserRole.Viewer)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            Name = name,
            Role = role,
            IsActive = true,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes a User from persistence. Use only in repositories.
    /// </summary>
    public static User Reconstitute(
        Guid id,
        string email,
        string name,
        UserRole role,
        bool isActive,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new User
        {
            Id = id,
            Email = email,
            Name = name,
            Role = role,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        SetUpdated();
    }

    public void UpdateProfile(string name)
    {
        Name = name;
        SetUpdated();
    }

    public void ChangeRole(UserRole role)
    {
        Role = role;
        SetUpdated();
    }

    public void SetPermissions(List<string> permissions)
    {
        Permissions = permissions;
        SetUpdated();
    }

    public void VerifyEmail()
    {
        EmailVerified = true;
        SetUpdated();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutUntil = null;
        SetUpdated();
    }

    public void RecordFailedLogin(int maxAttempts = 5, int lockoutMinutes = 15)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
        }
        SetUpdated();
    }

    public bool IsLockedOut => LockoutUntil.HasValue && LockoutUntil.Value > DateTime.UtcNow;

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}

public enum UserRole
{
    Viewer,
    Contributor,
    Analyst,
    Admin,
    SystemAdmin
}
