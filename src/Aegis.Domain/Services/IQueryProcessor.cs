namespace Aegis.Domain.Services;

/// <summary>
/// Service for processing and analyzing user queries
/// </summary>
public interface IQueryProcessor
{
    /// <summary>
    /// Process a user query and extract intent, entities, and metadata
    /// </summary>
    Task<QueryAnalysis> ProcessQueryAsync(string query, Guid? workspaceId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Analysis result of a user query
/// </summary>
public class QueryAnalysis
{
    public required string OriginalQuery { get; init; }
    public required string ProcessedQuery { get; init; }
    public QueryIntent Intent { get; init; }
    public List<string> ExtractedEntities { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
    public QueryComplexity Complexity { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Type of query intent
/// </summary>
public enum QueryIntent
{
    Question,           // User asking a question
    Command,            // User giving a command
    Search,             // User searching for information
    Analysis,           // User requesting analysis
    Summarization,      // User requesting summary
    Comparison,         // User comparing things
    Unknown
}

/// <summary>
/// Complexity level of the query
/// </summary>
public enum QueryComplexity
{
    Simple,     // Single-hop, straightforward query
    Medium,     // Multi-hop or requires context
    Complex     // Multi-step reasoning or synthesis required
}
