using System.Diagnostics;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Aegis.Infrastructure.Services.Connectors;

/// <summary>
/// Connector for PostgreSQL databases
/// </summary>
public class PostgreSqlConnector : IDataConnector
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<PostgreSqlConnector> _logger;

    public string ConnectorType => "postgresql";

    public PostgreSqlConnector(
        IDocumentRepository documentRepository,
        ILogger<PostgreSqlConnector> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public Task<Result> ValidateSettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        var required = new[] { "host", "port", "database", "username", "password", "table", "primary_key_column" };
        var missing = required.Where(key => !settings.ContainsKey(key) || string.IsNullOrWhiteSpace(settings[key])).ToList();

        if (missing.Any())
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("PostgreSql.InvalidSettings", $"Missing required settings: {string.Join(", ", missing)}")));
        }

        if (!int.TryParse(settings["port"], out _))
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("PostgreSql.InvalidPort", "Port must be a valid integer")));
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
            var connectionString = BuildConnectionString(settings);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Test that the table exists
            var table = settings["table"];
            var pkColumn = settings["primary_key_column"];

            await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table} LIMIT 1", connection);
            var count = await cmd.ExecuteScalarAsync(cancellationToken);

            var metadata = new Dictionary<string, string>
            {
                { "table", table },
                { "primary_key_column", pkColumn },
                { "database", settings["database"] }
            };

            return Result<ConnectionTestResult>.Success(new ConnectionTestResult(
                IsSuccessful: true,
                Message: $"Successfully connected to PostgreSQL database '{settings["database"]}' and verified table '{table}' exists",
                Metadata: metadata));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to PostgreSQL database");
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
            var connectionString = BuildConnectionString(settings);
            var table = settings["table"];
            var pkColumn = settings["primary_key_column"];
            var contentColumn = settings.GetValueOrDefault("content_column", "*"); // Default to all columns

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Build query with cursor support for incremental sync
            string query;
            if (!options.IsFullSync && !string.IsNullOrEmpty(options.LastSyncCursor))
            {
                // Incremental sync: only fetch records modified after the cursor
                var timestampColumn = settings.GetValueOrDefault("timestamp_column", "updated_at");
                query = $"SELECT * FROM {table} WHERE {timestampColumn} > @cursor ORDER BY {timestampColumn} LIMIT @limit";
            }
            else
            {
                // Full sync: fetch all records
                query = $"SELECT * FROM {table} ORDER BY {pkColumn} LIMIT @limit";
            }

            await using var cmd = new NpgsqlCommand(query, connection);
            if (!options.IsFullSync && !string.IsNullOrEmpty(options.LastSyncCursor))
            {
                cmd.Parameters.AddWithValue("cursor", DateTime.Parse(options.LastSyncCursor));
            }
            cmd.Parameters.AddWithValue("limit", options.BatchSize);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            string? lastTimestamp = null;

            while (await reader.ReadAsync(cancellationToken))
            {
                try
                {
                    processed++;

                    // Extract primary key
                    var pkValue = reader[pkColumn].ToString();
                    if (string.IsNullOrEmpty(pkValue))
                    {
                        failed++;
                        continue;
                    }

                    // Build document content from all columns
                    var rowData = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var fieldName = reader.GetName(i);
                        var fieldValue = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rowData[fieldName] = fieldValue;
                    }

                    var content = JsonSerializer.Serialize(rowData, new JsonSerializerOptions { WriteIndented = true });
                    var externalId = $"{table}:{pkValue}";

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
                        var document = Document.Create(
                            fileName: $"{table}_{pkValue}",
                            contentType: "application/json",
                            sizeBytes: content.Length,
                            dataSourceId: dataSourceId,
                            uploadedBy: Guid.Empty, // System-generated from connector
                            content: content,
                            externalId: externalId);

                        await _documentRepository.AddAsync(document, cancellationToken);
                        added++;
                    }

                    // Track last timestamp for cursor
                    if (settings.ContainsKey("timestamp_column"))
                    {
                        var timestampColumn = settings["timestamp_column"];
                        var timestampIndex = reader.GetOrdinal(timestampColumn);
                        if (!reader.IsDBNull(timestampIndex))
                        {
                            lastTimestamp = reader.GetValue(timestampIndex).ToString();
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
                    _logger.LogError(ex, "Failed to process row");
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
            _logger.LogError(ex, "Sync failed for PostgreSQL data source {DataSourceId}", dataSourceId);
            return Result<SyncResult>.Failure(Error.Internal("PostgreSql.SyncFailed", ex.Message));
        }
    }

    private string BuildConnectionString(Dictionary<string, string> settings)
    {
        return $"Host={settings["host"]};Port={settings["port"]};Database={settings["database"]};Username={settings["username"]};Password={settings["password"]}";
    }
}
