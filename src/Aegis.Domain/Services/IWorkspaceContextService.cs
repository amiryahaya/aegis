namespace Aegis.Domain.Services;

/// <summary>
/// Service for assembling workspace context including custom instructions and knowledge base
/// </summary>
public interface IWorkspaceContextService
{
    /// <summary>
    /// Assembles the full workspace context including custom instructions, entities, findings, and facts
    /// </summary>
    Task<WorkspaceContext> GetWorkspaceContextAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assembles workspace context relevant to a specific query
    /// </summary>
    Task<WorkspaceContext> GetRelevantContextAsync(
        Guid workspaceId,
        string query,
        int maxEntities = 10,
        int maxFindings = 5,
        int maxFacts = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Workspace context for RAG queries
/// </summary>
public class WorkspaceContext
{
    public string? CustomInstructions { get; init; }
    public List<WorkspaceEntityContext> Entities { get; init; } = new();
    public List<WorkspaceFindingContext> Findings { get; init; } = new();
    public List<WorkspaceFactContext> Facts { get; init; } = new();

    /// <summary>
    /// Formats the context as a string suitable for inclusion in RAG prompts
    /// </summary>
    public string FormatAsPromptContext()
    {
        var sections = new List<string>();

        if (!string.IsNullOrWhiteSpace(CustomInstructions))
        {
            sections.Add($"# Custom Instructions\n{CustomInstructions}");
        }

        if (Entities.Any())
        {
            var entitiesText = string.Join("\n", Entities.Select(e => e.FormatAsText()));
            sections.Add($"# Known Entities\n{entitiesText}");
        }

        if (Facts.Any())
        {
            var factsText = string.Join("\n", Facts.Select(f => f.FormatAsText()));
            sections.Add($"# Established Facts\n{factsText}");
        }

        if (Findings.Any())
        {
            var findingsText = string.Join("\n", Findings.Select(f => f.FormatAsText()));
            sections.Add($"# Key Findings\n{findingsText}");
        }

        return sections.Any() ? string.Join("\n\n", sections) : string.Empty;
    }
}

public class WorkspaceEntityContext
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public List<string> Aliases { get; init; } = new();
    public required string Confidence { get; init; }

    public string FormatAsText()
    {
        var parts = new List<string> { $"- {Name} ({Type})" };

        if (!string.IsNullOrWhiteSpace(Description))
            parts.Add($"  Description: {Description}");

        if (Aliases.Any())
            parts.Add($"  Also known as: {string.Join(", ", Aliases)}");

        parts.Add($"  Confidence: {Confidence}");

        return string.Join("\n", parts);
    }
}

public class WorkspaceFindingContext
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string Type { get; init; }

    public string FormatAsText()
    {
        return $"- [{Type}] {Title}\n  {Content}";
    }
}

public class WorkspaceFactContext
{
    public required Guid Id { get; init; }
    public required string Statement { get; init; }
    public required string Confidence { get; init; }

    public string FormatAsText()
    {
        return $"- {Statement} (Confidence: {Confidence})";
    }
}
