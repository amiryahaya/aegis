using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Services.NER;

/// <summary>
/// Intelligence-focused NER service that detects threat intelligence entities
/// </summary>
public class IntelligenceNERService : INERService
{
    private readonly INERService _basicNER;
    private readonly ILogger<IntelligenceNERService> _logger;

    // Intelligence-specific regex patterns
    private static readonly Regex CvePattern = new(
        @"\bCVE-\d{4}-\d{4,7}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Md5Pattern = new(
        @"\b[a-fA-F0-9]{32}\b",
        RegexOptions.Compiled);

    private static readonly Regex Sha1Pattern = new(
        @"\b[a-fA-F0-9]{40}\b",
        RegexOptions.Compiled);

    private static readonly Regex Sha256Pattern = new(
        @"\b[a-fA-F0-9]{64}\b",
        RegexOptions.Compiled);

    private static readonly Regex MitreTechniquePattern = new(
        @"\bT\d{4}(?:\.\d{3})?\b",
        RegexOptions.Compiled);

    private static readonly Regex DomainPattern = new(
        @"\b(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IntelligenceNERService(INERService basicNER, ILogger<IntelligenceNERService> logger)
    {
        _basicNER = basicNER;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<NamedEntity>>> ExtractEntitiesAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (text == null)
        {
            return Result<IReadOnlyList<NamedEntity>>.Failure(
                Error.Validation("NERService.NullText", "Text cannot be null"));
        }

        try
        {
            // Get basic entities first
            var basicResult = await _basicNER.ExtractEntitiesAsync(text, cancellationToken);
            if (basicResult.IsFailure)
            {
                return basicResult;
            }

            var entities = basicResult.Value.ToList();

            // Extract intelligence-specific entities
            await Task.Run(() =>
            {
                // Extract CVEs
                ExtractPattern(text, CvePattern, EntityType.Other, entities, 0.98, "CVE");

                // Extract file hashes (prioritize longer hashes)
                ExtractPattern(text, Sha256Pattern, EntityType.Other, entities, 0.95, "SHA256");
                ExtractPattern(text, Sha1Pattern, EntityType.Other, entities, 0.95, "SHA1");
                ExtractPattern(text, Md5Pattern, EntityType.Other, entities, 0.95, "MD5");

                // Extract MITRE ATT&CK techniques
                ExtractPattern(text, MitreTechniquePattern, EntityType.Other, entities, 0.90, "MITRE");

                // Extract domain names (but filter out already detected URLs)
                var urlPositions = entities
                    .Where(e => e.Type == EntityType.Url)
                    .Select(e => (e.StartPosition, e.EndPosition))
                    .ToHashSet();

                var domainMatches = DomainPattern.Matches(text);
                foreach (Match match in domainMatches)
                {
                    // Skip if this domain is part of a URL or email
                    var overlapsWithUrl = urlPositions.Any(pos =>
                        match.Index >= pos.StartPosition && match.Index < pos.EndPosition);

                    var overlapsWithEmail = entities.Any(e =>
                        e.Type == EntityType.Email &&
                        match.Index >= e.StartPosition &&
                        match.Index < e.EndPosition);

                    if (!overlapsWithUrl && !overlapsWithEmail)
                    {
                        entities.Add(new NamedEntity
                        {
                            Text = match.Value,
                            Type = EntityType.Other,
                            Confidence = 0.70,
                            StartPosition = match.Index,
                            EndPosition = match.Index + match.Length
                        });
                    }
                }
            }, cancellationToken);

            // Remove duplicates (keep highest confidence)
            var uniqueEntities = entities
                .GroupBy(e => (e.Text, e.Type))
                .Select(g => g.OrderByDescending(e => e.Confidence).First())
                .OrderBy(e => e.StartPosition)
                .ToList();

            return Result<IReadOnlyList<NamedEntity>>.Success(uniqueEntities.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract intelligence entities");
            return Result<IReadOnlyList<NamedEntity>>.Failure(
                Error.Internal("NERService.ExtractionError", "Failed to extract intelligence entities"));
        }
    }

    private void ExtractPattern(
        string text,
        Regex pattern,
        EntityType type,
        List<NamedEntity> entities,
        double confidence,
        string subType)
    {
        var matches = pattern.Matches(text);

        foreach (Match match in matches)
        {
            entities.Add(new NamedEntity
            {
                Text = match.Value,
                Type = type,
                Confidence = confidence,
                StartPosition = match.Index,
                EndPosition = match.Index + match.Length
            });
        }
    }
}
