using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Core abstraction for all data connectors
/// </summary>
public interface IDataConnector
{
    /// <summary>
    /// Unique identifier for the connector type (e.g., "postgresql", "mongodb", "rss")
    /// </summary>
    string ConnectorType { get; }

    /// <summary>
    /// Validates connection settings without connecting
    /// </summary>
    Task<Result> ValidateSettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection with provided settings
    /// </summary>
    Task<Result<ConnectionTestResult>> TestConnectionAsync(Dictionary<string, string> settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full or incremental sync
    /// </summary>
    Task<Result<SyncResult>> SyncAsync(Guid dataSourceId, Dictionary<string, string> settings, SyncOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of connection test
/// </summary>
public record ConnectionTestResult(
    bool IsSuccessful,
    string Message,
    Dictionary<string, string> Metadata);

/// <summary>
/// Options for sync operation
/// </summary>
public record SyncOptions(
    bool IsFullSync,
    string? LastSyncCursor,
    int BatchSize = 100,
    Action<SyncProgress>? ProgressCallback = null);

/// <summary>
/// Progress update during sync
/// </summary>
public record SyncProgress(
    int Processed,
    int Added,
    int Updated,
    int Failed,
    string? CurrentItem);

/// <summary>
/// Result of sync operation
/// </summary>
public record SyncResult(
    int DocumentsAdded,
    int DocumentsUpdated,
    int DocumentsDeleted,
    int DocumentsFailed,
    string? NextCursor,
    TimeSpan Duration);
