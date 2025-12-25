using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Services.NER;

/// <summary>
/// Basic pattern-based NER service using regular expressions
/// </summary>
public class BasicNERService : INERService
{
    private readonly ILogger<BasicNERService> _logger;

    // Regex patterns for entity detection
    private static readonly Regex EmailPattern = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        RegexOptions.Compiled);

    private static readonly Regex UrlPattern = new(
        @"\b(https?://|www\.)[^\s<>""]+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IpAddressPattern = new(
        @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"\b(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3,4}[-.\s]?\d{4}\b|\b\+?1[-.\s]?\d{3}[-.\s]?\d{4}\b",
        RegexOptions.Compiled);

    public BasicNERService(ILogger<BasicNERService> logger)
    {
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
            return await Task.Run(() =>
            {
                var entities = new List<NamedEntity>();

                // Extract emails
                ExtractPattern(text, EmailPattern, NEREntityType.Email, entities, 0.95);

                // Extract URLs
                ExtractPattern(text, UrlPattern, NEREntityType.Url, entities, 0.90);

                // Extract IP addresses
                ExtractPattern(text, IpAddressPattern, NEREntityType.IpAddress, entities, 0.95);

                // Extract phone numbers
                ExtractPattern(text, PhonePattern, NEREntityType.PhoneNumber, entities, 0.85);

                return Result<IReadOnlyList<NamedEntity>>.Success(entities.AsReadOnly());
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract named entities");
            return Result<IReadOnlyList<NamedEntity>>.Failure(
                Error.Internal("NERService.ExtractionError", "Failed to extract named entities"));
        }
    }

    private void ExtractPattern(
        string text,
        Regex pattern,
        NEREntityType type,
        List<NamedEntity> entities,
        double confidence)
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
