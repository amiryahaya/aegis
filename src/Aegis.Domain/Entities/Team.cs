using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class Team : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedBy { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<TeamMember> _members = [];
    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    private Team() { } // For ORM

    public static Team Create(string name, Guid createdBy, string? description = null)
    {
        return new Team
        {
            Id = UuidGenerator.NewId(),
            Name = name,
            Description = description,
            CreatedBy = createdBy,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes a Team from persistence. Use only in repositories.
    /// </summary>
    public static Team Reconstitute(
        Guid id,
        string name,
        string? description,
        Guid createdBy,
        bool isActive,
        DateTime createdAt,
        DateTime? updatedAt,
        IEnumerable<TeamMember>? members = null)
    {
        var team = new Team
        {
            Id = id,
            Name = name,
            Description = description,
            CreatedBy = createdBy,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        if (members != null)
        {
            team._members.AddRange(members);
        }

        return team;
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
        SetUpdated();
    }

    public Result AddMember(Guid userId, TeamRole role)
    {
        if (_members.Any(m => m.UserId == userId))
        {
            return Result.Failure(TeamErrors.MemberAlreadyExists);
        }

        _members.Add(TeamMember.Create(Id, userId, role));
        IncrementVersion();
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(TeamErrors.MemberNotFound);
        }

        _members.Remove(member);
        IncrementVersion();
        return Result.Success();
    }

    public Result ChangeMemberRole(Guid userId, TeamRole newRole)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(TeamErrors.MemberNotFound);
        }

        member.ChangeRole(newRole);
        IncrementVersion();
        return Result.Success();
    }

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

public class TeamMember
{
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public TeamRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private TeamMember() { } // For ORM

    public static TeamMember Create(Guid teamId, Guid userId, TeamRole role)
    {
        return new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
    }

    public static TeamMember Reconstitute(Guid teamId, Guid userId, TeamRole role, DateTime joinedAt)
    {
        return new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            Role = role,
            JoinedAt = joinedAt
        };
    }

    public void ChangeRole(TeamRole role)
    {
        Role = role;
    }
}

public enum TeamRole
{
    Member,
    Admin,
    Owner
}

public static class TeamErrors
{
    public static readonly Error MemberAlreadyExists = Error.Conflict("Team.MemberAlreadyExists", "User is already a member of this team");
    public static readonly Error MemberNotFound = Error.NotFound("Team.MemberNotFound", "User is not a member of this team");
    public static readonly Error NotFound = Error.NotFound("Team.NotFound", "Team not found");
}
