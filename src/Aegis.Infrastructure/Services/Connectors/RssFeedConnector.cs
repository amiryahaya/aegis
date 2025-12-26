using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Connectors;

/// <summary>
/// Connector for RSS/Atom feeds
/// </summary>
public class RssFeedConnector : IDataConnector
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<RssFeedConnector> _logger;
    private readonly HttpClient _httpClient;

    public string ConnectorType => "rssfeed";

    public RssFeedConnector(
        IDocumentRepository documentRepository,
        ILogger<RssFeedConnector> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _documentRepository = documentRepository;
        _logger = logger;
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
    }

    public Task<Result> ValidateSettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        var required = new[] { "feed_url" };
        var missing = required.Where(key => !settings.ContainsKey(key) || string.IsNullOrWhiteSpace(settings[key])).ToList();

        if (missing.Any())
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("RssFeed.InvalidSettings", $"Missing required settings: {string.Join(", ", missing)}")));
        }

        if (!Uri.TryCreate(settings["feed_url"], UriKind.Absolute, out _))
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("RssFeed.InvalidUrl", "feed_url must be a valid URL")));
        }

        return Task.FromResult(Result.Success());
    }

    public async Task<Result<ConnectionTestResult>> TestConnectionAsync(Dictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateSettingsAsync(settings, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<ConnectionTestResult>.Failure(validationResult.Error!);
        }

        try
        {
            var feedUrl = settings["feed_url"];

            using var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = XmlReader.Create(stream);

            var feed = SyndicationFeed.Load(reader);

            var itemCount = feed.Items.Count();
            var metadata = new Dictionary<string, string>
            {
                { "feed_url", feedUrl },
                { "feed_title", feed.Title?.Text ?? "Unknown" },
                { "item_count", itemCount.ToString() }
            };

            return Result<ConnectionTestResult>.Success(new ConnectionTestResult(
                IsSuccessful: true,
                Message: $"Successfully connected to RSS feed '{feed.Title?.Text}' ({itemCount} items)",
                Metadata: metadata));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RSS feed");
            return Result<ConnectionTestResult>.Success(new ConnectionTestResult(
                IsSuccessful: false,
                Message: $"Connection failed: {ex.Message}",
                Metadata: new Dictionary<string, string>()));
        }
    }

    public async Task<Result<SyncResult>> SyncAsync(Guid dataSourceId, Dictionary<string, string> settings, SyncOptions options, CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateSettingsAsync(settings, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<SyncResult>.Failure(validationResult.Error!);
        }

        var stopwatch = Stopwatch.StartNew();
        var added = 0;
        var updated = 0;
        var failed = 0;
        var processed = 0;

        try
        {
            var feedUrl = settings["feed_url"];

            using var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = XmlReader.Create(stream);

            var feed = SyndicationFeed.Load(reader);
            var items = feed.Items.ToList();

            // Filter by cursor if incremental sync
            DateTime? cursorDate = null;
            if (!options.IsFullSync && !string.IsNullOrEmpty(options.LastSyncCursor))
            {
                if (DateTime.TryParse(options.LastSyncCursor, out var parsedDate))
                {
                    cursorDate = parsedDate;
                    items = items.Where(item => item.PublishDate > cursorDate).ToList();
                }
            }

            // Sort by publish date (oldest first) and limit
            items = items.OrderBy(item => item.PublishDate).Take(options.BatchSize).ToList();

            DateTime? lastPublishDate = null;

            foreach (var item in items)
            {
                try
                {
                    processed++;

                    // Extract item ID (use link or generate from title)
                    var itemId = item.Id ?? item.Links.FirstOrDefault()?.Uri?.ToString() ?? Guid.NewGuid().ToString();
                    var externalId = $"rss:{Uri.EscapeDataString(itemId)}";

                    // Build item data
                    var itemData = new Dictionary<string, object?>
                    {
                        { "id", itemId },
                        { "title", item.Title?.Text },
                        { "summary", item.Summary?.Text },
                        { "content", GetItemContent(item) },
                        { "link", item.Links.FirstOrDefault()?.Uri?.ToString() },
                        { "publish_date", item.PublishDate.UtcDateTime },
                        { "last_updated", item.LastUpdatedTime.UtcDateTime },
                        { "authors", item.Authors.Select(a => a.Name).ToList() },
                        { "categories", item.Categories.Select(c => c.Name).ToList() }
                    };

                    var content = JsonSerializer.Serialize(itemData, new JsonSerializerOptions { WriteIndented = true });

                    // Check if document already exists
                    var existingDoc = await _documentRepository.GetByExternalIdAsync(dataSourceId, externalId, cancellationToken);

                    if (existingDoc != null)
                    {
                        // Update existing document
                        existingDoc.UpdateContent(content, content.Length);
                        await _documentRepository.UpdateAsync(existingDoc, cancellationToken);
                        updated++;
                    }
                    else
                    {
                        // Create new document
                        var fileName = $"{SanitizeFileName(item.Title?.Text ?? "rss_item")}_{item.PublishDate:yyyyMMdd}";
                        var newDoc = Document.Create(
                            fileName: fileName,
                            contentType: "application/json",
                            sizeBytes: content.Length,
                            dataSourceId: dataSourceId,
                            uploadedBy: Guid.Empty, // System-generated from connector
                            title: item.Title?.Text,
                            content: content,
                            externalId: externalId);

                        await _documentRepository.AddAsync(newDoc, cancellationToken);
                        added++;
                    }

                    // Track last publish date for cursor
                    lastPublishDate = item.PublishDate.UtcDateTime;

                    // Report progress
                    options.ProgressCallback?.Invoke(new SyncProgress(
                        Processed: processed,
                        Added: added,
                        Updated: updated,
                        Failed: failed,
                        CurrentItem: item.Title?.Text ?? externalId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process RSS feed item");
                    failed++;
                }
            }

            stopwatch.Stop();

            return Result<SyncResult>.Success(new SyncResult(
                DocumentsAdded: added,
                DocumentsUpdated: updated,
                DocumentsDeleted: 0,
                DocumentsFailed: failed,
                NextCursor: lastPublishDate?.ToString("o"),
                Duration: stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for RSS feed data source {DataSourceId}", dataSourceId);
            return Result<SyncResult>.Failure(Error.Internal("RssFeed.SyncFailed", ex.Message));
        }
    }

    private string GetItemContent(SyndicationItem item)
    {
        // Try to get content from various possible locations
        if (item.Content is TextSyndicationContent textContent)
        {
            return textContent.Text;
        }

        if (item.Content is UrlSyndicationContent urlContent)
        {
            return urlContent.Url?.ToString() ?? string.Empty;
        }

        // Fall back to summary
        return item.Summary?.Text ?? string.Empty;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}
