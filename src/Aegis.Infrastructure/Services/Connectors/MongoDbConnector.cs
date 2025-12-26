using System.Diagnostics;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aegis.Infrastructure.Services.Connectors;

/// <summary>
/// Connector for MongoDB databases
/// </summary>
public class MongoDbConnector : IDataConnector
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<MongoDbConnector> _logger;

    public string ConnectorType => "mongodb";

    public MongoDbConnector(
        IDocumentRepository documentRepository,
        ILogger<MongoDbConnector> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public Task<Result> ValidateSettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        var required = new[] { "connection_string", "database", "collection" };
        var missing = required.Where(key => !settings.ContainsKey(key) || string.IsNullOrWhiteSpace(settings[key])).ToList();

        if (missing.Any())
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("MongoDB.InvalidSettings", $"Missing required settings: {string.Join(", ", missing)}")));
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
            var connectionString = settings["connection_string"];
            var databaseName = settings["database"];
            var collectionName = settings["collection"];

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);

            // Test connection by pinging the database
            await database.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: cancellationToken);

            // Check if collection exists
            var collectionExists = await CollectionExistsAsync(database, collectionName, cancellationToken);
            if (!collectionExists)
            {
                return Result<ConnectionTestResult>.Success(new ConnectionTestResult(
                    IsSuccessful: false,
                    Message: $"Collection '{collectionName}' does not exist in database '{databaseName}'",
                    Metadata: new Dictionary<string, string>()));
            }

            // Get collection stats
            var collection = database.GetCollection<BsonDocument>(collectionName);
            var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken);

            var metadata = new Dictionary<string, string>
            {
                { "database", databaseName },
                { "collection", collectionName },
                { "document_count", count.ToString() }
            };

            return Result<ConnectionTestResult>.Success(new ConnectionTestResult(
                IsSuccessful: true,
                Message: $"Successfully connected to MongoDB database '{databaseName}', collection '{collectionName}' ({count} documents)",
                Metadata: metadata));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MongoDB database");
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
            var connectionString = settings["connection_string"];
            var databaseName = settings["database"];
            var collectionName = settings["collection"];
            var idField = settings.GetValueOrDefault("id_field", "_id");
            var timestampField = settings.GetValueOrDefault("timestamp_field");

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Build filter for incremental sync
            FilterDefinition<BsonDocument> filter = FilterDefinition<BsonDocument>.Empty;
            if (!options.IsFullSync && !string.IsNullOrEmpty(options.LastSyncCursor) && !string.IsNullOrEmpty(timestampField))
            {
                var cursorDate = DateTime.Parse(options.LastSyncCursor);
                filter = Builders<BsonDocument>.Filter.Gt(timestampField, cursorDate);
            }

            // Build sort
            SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending(idField);
            if (!string.IsNullOrEmpty(timestampField))
            {
                sort = Builders<BsonDocument>.Sort.Ascending(timestampField);
            }

            // Query documents
            var findOptions = new FindOptions<BsonDocument>
            {
                Limit = options.BatchSize,
                Sort = sort
            };

            var cursor = await collection.FindAsync(filter, findOptions, cancellationToken);
            var documents = await cursor.ToListAsync(cancellationToken);

            string? lastTimestamp = null;

            foreach (var doc in documents)
            {
                try
                {
                    processed++;

                    // Extract ID
                    var docId = doc.GetValue(idField, BsonNull.Value).ToString();
                    if (string.IsNullOrEmpty(docId) || docId == "BsonNull")
                    {
                        failed++;
                        continue;
                    }

                    // Convert BSON to JSON
                    var jsonSettings = new MongoDB.Bson.IO.JsonWriterSettings { OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson };
                    var content = doc.ToJson(jsonSettings);
                    var externalId = $"{collectionName}:{docId}";

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
                        var newDoc = Document.Create(
                            fileName: $"{collectionName}_{docId}",
                            contentType: "application/json",
                            sizeBytes: content.Length,
                            dataSourceId: dataSourceId,
                            uploadedBy: Guid.Empty, // System-generated from connector
                            content: content,
                            externalId: externalId);

                        await _documentRepository.AddAsync(newDoc, cancellationToken);
                        added++;
                    }

                    // Track last timestamp for cursor
                    if (!string.IsNullOrEmpty(timestampField) && doc.Contains(timestampField))
                    {
                        var timestamp = doc.GetValue(timestampField, BsonNull.Value);
                        if (timestamp != BsonNull.Value)
                        {
                            if (timestamp.IsValidDateTime)
                            {
                                lastTimestamp = timestamp.ToUniversalTime().ToString("o");
                            }
                            else
                            {
                                lastTimestamp = timestamp.ToString();
                            }
                        }
                    }

                    // Report progress
                    options.ProgressCallback?.Invoke(new SyncProgress(
                        Processed: processed,
                        Added: added,
                        Updated: updated,
                        Failed: failed,
                        CurrentItem: externalId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process MongoDB document");
                    failed++;
                }
            }

            stopwatch.Stop();

            return Result<SyncResult>.Success(new SyncResult(
                DocumentsAdded: added,
                DocumentsUpdated: updated,
                DocumentsDeleted: 0,
                DocumentsFailed: failed,
                NextCursor: lastTimestamp,
                Duration: stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for MongoDB data source {DataSourceId}", dataSourceId);
            return Result<SyncResult>.Failure(Error.Internal("MongoDB.SyncFailed", ex.Message));
        }
    }

    private async Task<bool> CollectionExistsAsync(IMongoDatabase database, string collectionName, CancellationToken cancellationToken)
    {
        var filter = new BsonDocument("name", collectionName);
        var collections = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter }, cancellationToken);
        return await collections.AnyAsync(cancellationToken);
    }
}
