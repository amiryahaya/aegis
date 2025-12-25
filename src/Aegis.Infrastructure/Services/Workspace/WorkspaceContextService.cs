using Aegis.Domain.Repositories;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Workspace;

public class WorkspaceContextService : IWorkspaceContextService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceEntityRepository _entityRepository;
    private readonly IWorkspaceFindingRepository _findingRepository;
    private readonly IWorkspaceFactRepository _factRepository;

    public WorkspaceContextService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceEntityRepository entityRepository,
        IWorkspaceFindingRepository findingRepository,
        IWorkspaceFactRepository factRepository)
    {
        _workspaceRepository = workspaceRepository;
        _entityRepository = entityRepository;
        _findingRepository = findingRepository;
        _factRepository = factRepository;
    }

    public async Task<WorkspaceContext> GetWorkspaceContextAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        // Get workspace for custom instructions
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);

        // Get all knowledge base content
        var entities = await _entityRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var findings = await _findingRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var facts = await _factRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);

        return new WorkspaceContext
        {
            CustomInstructions = workspace?.CustomInstructions,
            Entities = entities.Select(e => new WorkspaceEntityContext
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type,
                Description = e.Description,
                Aliases = e.Aliases,
                Confidence = e.Confidence
            }).ToList(),
            Findings = findings.Select(f => new WorkspaceFindingContext
            {
                Id = f.Id,
                Title = f.Title,
                Content = f.Content,
                Type = f.Type
            }).ToList(),
            Facts = facts.Select(f => new WorkspaceFactContext
            {
                Id = f.Id,
                Statement = f.Statement,
                Confidence = f.Confidence
            }).ToList()
        };
    }

    public async Task<WorkspaceContext> GetRelevantContextAsync(
        Guid workspaceId,
        string query,
        int maxEntities = 10,
        int maxFindings = 5,
        int maxFacts = 10,
        CancellationToken cancellationToken = default)
    {
        // Get workspace for custom instructions
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);

        // Get all knowledge base content
        var allEntities = await _entityRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var allFindings = await _findingRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var allFacts = await _factRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);

        // Simple relevance filtering based on query terms
        var queryTerms = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 3) // Only consider terms longer than 3 chars
            .ToHashSet();

        // Filter entities based on name, description, or aliases
        var relevantEntities = allEntities
            .Select(e => new
            {
                Entity = e,
                Score = CalculateRelevanceScore(queryTerms,
                    e.Name,
                    e.Description ?? string.Empty,
                    string.Join(" ", e.Aliases))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxEntities)
            .Select(x => x.Entity);

        // Filter findings based on title and content
        var relevantFindings = allFindings
            .Select(f => new
            {
                Finding = f,
                Score = CalculateRelevanceScore(queryTerms, f.Title, f.Content)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxFindings)
            .Select(x => x.Finding);

        // Filter facts based on statement
        var relevantFacts = allFacts
            .Select(f => new
            {
                Fact = f,
                Score = CalculateRelevanceScore(queryTerms, f.Statement)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxFacts)
            .Select(x => x.Fact);

        return new WorkspaceContext
        {
            CustomInstructions = workspace?.CustomInstructions,
            Entities = relevantEntities.Select(e => new WorkspaceEntityContext
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type,
                Description = e.Description,
                Aliases = e.Aliases,
                Confidence = e.Confidence
            }).ToList(),
            Findings = relevantFindings.Select(f => new WorkspaceFindingContext
            {
                Id = f.Id,
                Title = f.Title,
                Content = f.Content,
                Type = f.Type
            }).ToList(),
            Facts = relevantFacts.Select(f => new WorkspaceFactContext
            {
                Id = f.Id,
                Statement = f.Statement,
                Confidence = f.Confidence
            }).ToList()
        };
    }

    private static int CalculateRelevanceScore(HashSet<string> queryTerms, params string[] textFields)
    {
        var score = 0;
        var combinedText = string.Join(" ", textFields).ToLowerInvariant();

        foreach (var term in queryTerms)
        {
            if (combinedText.Contains(term))
            {
                score++;
            }
        }

        return score;
    }
}
