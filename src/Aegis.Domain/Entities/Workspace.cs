using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class Workspace : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public WorkspaceStatus Status { get; private set; }

    private Workspace() { } // For ORM

    public static Workspace Create(
        string name,
        Guid createdBy,
        string? description = null,
        Guid? teamId = null)
    {
        return new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            TeamId = teamId,
            CreatedBy = createdBy,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
        SetUpdated();
    }

    public void AssignToTeam(Guid teamId)
    {
        TeamId = teamId;
        SetUpdated();
    }

    public void RemoveFromTeam()
    {
        TeamId = null;
        SetUpdated();
    }

    public void Archive()
    {
        Status = WorkspaceStatus.Archived;
        SetUpdated();
    }

    public void Activate()
    {
        Status = WorkspaceStatus.Active;
        SetUpdated();
    }

    public void Delete()
    {
        Status = WorkspaceStatus.Deleted;
        SetUpdated();
    }
}

public enum WorkspaceStatus
{
    Active,
    Archived,
    Deleted
}

public static class WorkspaceErrors
{
    public static readonly Error NotFound = Error.NotFound("Workspace.NotFound", "Workspace not found");
    public static readonly Error Archived = Error.Validation("Workspace.Archived", "Cannot modify an archived workspace");
    public static readonly Error Deleted = Error.Validation("Workspace.Deleted", "Cannot access a deleted workspace");
}
