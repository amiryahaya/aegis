using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for Named Entity Recognition (NER)
/// </summary>
public interface INERService
{
    /// <summary>
    /// Extracts named entities from text
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing a list of recognized entities</returns>
    Task<Result<IReadOnlyList<NamedEntity>>> ExtractEntitiesAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a named entity extracted from text
/// </summary>
public class NamedEntity
{
    /// <summary>
    /// The entity text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The entity type (PERSON, ORGANIZATION, LOCATION, etc.)
    /// </summary>
    public required EntityType Type { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Start position in the original text
    /// </summary>
    public int StartPosition { get; init; }

    /// <summary>
    /// End position in the original text
    /// </summary>
    public int EndPosition { get; init; }
}

/// <summary>
/// Common entity types for NER
/// </summary>
public enum EntityType
{
    Person,
    Organization,
    Location,
    Date,
    Time,
    Money,
    Percentage,
    Email,
    PhoneNumber,
    Url,
    IpAddress,
    Other
}
