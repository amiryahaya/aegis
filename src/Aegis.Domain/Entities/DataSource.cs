using Aegis.Domain.Common;

namespace Aegis.Domain.Entities;

public class DataSource : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public DataSourceType Type { get; private set; }
    public DataSourceStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Dictionary<string, string> Settings { get; private set; } = new();
    public int DocumentCount { get; private set; }
    public long TotalSizeBytes { get; private set; }
    public DateTime? LastIndexedAt { get; private set; }

    private DataSource() { } // For ORM

    public static DataSource Create(
        string name,
        Guid teamId,
        Guid createdBy,
        DataSourceType type = DataSourceType.Upload,
        string? description = null,
        Guid? workspaceId = null)
    {
        return new DataSource
        {
            Id = UuidGenerator.NewId(),
            Name = name,
            Description = description,
            TeamId = teamId,
            WorkspaceId = workspaceId,
            Type = type,
            Status = DataSourceStatus.Active,
            CreatedBy = createdBy,
            DocumentCount = 0,
            TotalSizeBytes = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reconstitutes a DataSource from persistence. Use only in repositories.
    /// </summary>
    public static DataSource Reconstitute(
        Guid id,
        string name,
        string? description,
        Guid teamId,
        Guid? workspaceId,
        DataSourceType type,
        DataSourceStatus status,
        Guid createdBy,
        Dictionary<string, string> settings,
        int documentCount,
        long totalSizeBytes,
        DateTime? lastIndexedAt,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        return new DataSource
        {
            Id = id,
            Name = name,
            Description = description,
            TeamId = teamId,
            WorkspaceId = workspaceId,
            Type = type,
            Status = status,
            CreatedBy = createdBy,
            Settings = settings,
            DocumentCount = documentCount,
            TotalSizeBytes = totalSizeBytes,
            LastIndexedAt = lastIndexedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
        SetUpdated();
    }

    public void UpdateSettings(Dictionary<string, string> settings)
    {
        Settings = settings;
        SetUpdated();
    }

    public void IncrementDocumentCount(long sizeBytes)
    {
        DocumentCount++;
        TotalSizeBytes += sizeBytes;
        SetUpdated();
    }

    public void DecrementDocumentCount(long sizeBytes)
    {
        DocumentCount = Math.Max(0, DocumentCount - 1);
        TotalSizeBytes = Math.Max(0, TotalSizeBytes - sizeBytes);
        SetUpdated();
    }

    public void MarkIndexed()
    {
        LastIndexedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Activate()
    {
        Status = DataSourceStatus.Active;
        SetUpdated();
    }

    public void Deactivate()
    {
        Status = DataSourceStatus.Inactive;
        SetUpdated();
    }

    public void MarkIndexing()
    {
        Status = DataSourceStatus.Indexing;
        SetUpdated();
    }

    public void MarkError()
    {
        Status = DataSourceStatus.Error;
        SetUpdated();
    }
}

public enum DataSourceType
{
    Upload,
    PostgreSQL,
    MongoDB,
    MySQL,
    MSSQL,
    RssFeed,
    RestApi,
    GraphQLApi,
    WebScraper
}

public enum DataSourceStatus
{
    Active,
    Inactive,
    Indexing,
    Error
}

public static class DataSourceErrors
{
    public static readonly Error NotFound = Error.NotFound("DataSource.NotFound", "Data source not found");
    public static readonly Error Unauthorized = Error.Forbidden("DataSource.Unauthorized", "You don't have access to this data source");
    public static readonly Error AlreadyIndexing = Error.Validation("DataSource.AlreadyIndexing", "Data source is already being indexed");
}
