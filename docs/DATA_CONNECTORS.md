# Data Connectors Guide

## Overview

AEGIS supports multiple data source connectors for ingesting content from external systems. Each connector implements incremental sync capabilities to efficiently update content as it changes.

## Supported Connectors

### 1. PostgreSQL Connector

Connect to PostgreSQL databases and sync table data as documents.

#### Configuration

```json
{
  "name": "Production Database",
  "type": "PostgreSQL",
  "settings": {
    "host": "localhost",
    "port": "5432",
    "database": "mydb",
    "username": "user",
    "password": "password",
    "table": "articles",
    "primary_key_column": "id",
    "timestamp_column": "updated_at"  // Optional: for incremental sync
  }
}
```

#### Features
- **Full Sync**: Imports all rows from the specified table
- **Incremental Sync**: Only syncs rows modified since last sync (requires timestamp column)
- **Automatic ID Generation**: Uses primary key for external document ID
- **JSON Serialization**: Converts table rows to JSON for indexing

#### API Example

```bash
# Create data source
POST /api/data-sources
{
  "workspaceId": "uuid",
  "name": "Production Database",
  "type": "PostgreSQL",
  "settings": {
    "host": "db.example.com",
    "port": "5432",
    "database": "production",
    "username": "readonly",
    "password": "secret",
    "table": "knowledge_base",
    "primary_key_column": "id",
    "timestamp_column": "modified_at"
  }
}

# Trigger sync
POST /api/sync/{dataSourceId}/trigger

# Check sync status
GET /api/sync/{dataSourceId}/history
```

---

### 2. MongoDB Connector

Connect to MongoDB collections and sync documents.

#### Configuration

```json
{
  "name": "Product Catalog",
  "type": "MongoDB",
  "settings": {
    "connection_string": "mongodb://localhost:27017",
    "database": "ecommerce",
    "collection": "products",
    "timestamp_field": "updatedAt",  // Optional: for incremental sync
    "filter": "{\"status\": \"published\"}"  // Optional: MongoDB filter
  }
}
```

#### Features
- **BSON to JSON Conversion**: Automatically converts MongoDB BSON to JSON
- **Flexible Filtering**: Apply MongoDB query filters
- **Incremental Sync**: Track changes using timestamp fields
- **ObjectId Handling**: Preserves MongoDB ObjectIds

#### API Example

```bash
POST /api/data-sources
{
  "workspaceId": "uuid",
  "name": "Product Catalog",
  "type": "MongoDB",
  "settings": {
    "connection_string": "mongodb://mongo.example.com:27017",
    "database": "catalog",
    "collection": "products",
    "timestamp_field": "lastModified",
    "filter": "{\"category\": \"electronics\"}"
  }
}
```

---

### 3. RSS Feed Connector

Ingest content from RSS and Atom feeds.

#### Configuration

```json
{
  "name": "Tech News Feed",
  "type": "RssFeed",
  "settings": {
    "feed_url": "https://example.com/rss"
  }
}
```

#### Features
- **RSS 2.0 and Atom Support**: Compatible with both feed formats
- **Automatic Deduplication**: Tracks article IDs to prevent duplicates
- **Metadata Extraction**: Captures authors, categories, publish dates
- **Content Extraction**: Pulls full content or summary

#### API Example

```bash
POST /api/data-sources
{
  "workspaceId": "uuid",
  "name": "Industry News",
  "type": "RssFeed",
  "settings": {
    "feed_url": "https://techcrunch.com/feed/"
  }
}
```

---

## Sync Management

### Scheduled Sync

Configure automatic sync schedules using cron expressions:

```bash
POST /api/sync/{dataSourceId}/schedule
{
  "cronExpression": "0 0 * * *",  // Daily at midnight
  "isEnabled": true
}
```

**Common Cron Expressions:**
- `0 * * * *` - Every hour
- `0 0 * * *` - Daily at midnight
- `0 0 * * 0` - Weekly on Sunday
- `0 0 1 * *` - Monthly on the 1st

### Manual Sync

Trigger sync on-demand:

```bash
POST /api/sync/{dataSourceId}/trigger
{
  "isFullSync": false  // true for full sync, false for incremental
}
```

### Sync History

View sync execution history:

```bash
GET /api/sync/{dataSourceId}/history?limit=20
```

Response:
```json
{
  "items": [
    {
      "id": "uuid",
      "dataSourceId": "uuid",
      "status": "Completed",
      "documentsAdded": 45,
      "documentsUpdated": 12,
      "documentsFailed": 0,
      "startedAt": "2025-01-15T10:00:00Z",
      "completedAt": "2025-01-15T10:05:30Z",
      "duration": "00:05:30"
    }
  ]
}
```

---

## Architecture

### Connector Interface

All connectors implement `IDataConnector`:

```csharp
public interface IDataConnector
{
    string ConnectorType { get; }

    Task<Result> ValidateSettingsAsync(
        Dictionary<string, string> settings,
        CancellationToken cancellationToken = default);

    Task<Result<ConnectionTestResult>> TestConnectionAsync(
        Dictionary<string, string> settings,
        CancellationToken cancellationToken = default);

    Task<Result<SyncResult>> SyncAsync(
        Guid dataSourceId,
        Dictionary<string, string> settings,
        SyncOptions options,
        CancellationToken cancellationToken = default);
}
```

### Creating Custom Connectors

1. **Implement IDataConnector**:

```csharp
public class CustomConnector : IDataConnector
{
    public string ConnectorType => "custom";

    public async Task<Result> ValidateSettingsAsync(
        Dictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        // Validate required settings
        if (!settings.ContainsKey("api_key"))
            return Result.Failure(Error.Validation("Missing api_key"));

        return Result.Success();
    }

    public async Task<Result<ConnectionTestResult>> TestConnectionAsync(
        Dictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        // Test connection
        // Return success/failure with metadata
    }

    public async Task<Result<SyncResult>> SyncAsync(
        Guid dataSourceId,
        Dictionary<string, string> settings,
        SyncOptions options,
        CancellationToken cancellationToken)
    {
        // Implement sync logic
        // Create Document entities and add via IDocumentRepository
        // Return SyncResult with counts
    }
}
```

2. **Register in ServiceCollectionExtensions**:

```csharp
services.AddScoped<CustomConnector>();
services.AddSingleton<IDataConnectorFactory>(sp =>
{
    var factory = new DataConnectorFactory(sp);
    factory.RegisterConnector(DataSourceType.Custom, typeof(CustomConnector));
    return factory;
});
```

---

## Best Practices

### Connection Strings
- **Never commit credentials** - Use environment variables or secret management
- Store sensitive settings in `appsettings.json` (excluded from git):
  ```json
  {
    "DataSources": {
      "PostgreSQL": {
        "DefaultConnectionString": "Host=...;Password=${DB_PASSWORD}"
      }
    }
  }
  ```

### Sync Frequency
- **Hourly**: Fast-changing data (news feeds, social media)
- **Daily**: Moderate updates (product catalogs, CRM data)
- **Weekly**: Slow-changing data (policies, documentation)

### Performance
- Use **incremental sync** whenever possible
- Set appropriate **batch sizes** (default: 100 documents)
- Monitor **sync duration** and adjust frequency accordingly

### Error Handling
- Failed syncs are logged in SyncHistory
- Partial failures continue processing remaining documents
- Check sync history regularly for issues

---

## Troubleshooting

### Connection Failures

**PostgreSQL:**
```
Error: "Connection refused"
Solution: Check firewall, verify host/port, ensure PostgreSQL accepts remote connections
```

**MongoDB:**
```
Error: "Authentication failed"
Solution: Verify connection string format, check user permissions
```

**RSS Feed:**
```
Error: "Invalid feed format"
Solution: Validate feed URL in browser, check for CORS issues
```

### Sync Failures

Check sync history for detailed error messages:
```bash
GET /api/sync/{dataSourceId}/history
```

Common issues:
- **Timeout**: Reduce batch size or increase timeout
- **Schema changes**: Update connector settings for new columns/fields
- **Network issues**: Check connectivity, retry failed sync

---

## Environment Variables

```bash
# PostgreSQL
DB_HOST=localhost
DB_PORT=5432
DB_NAME=aegis
DB_USER=postgres
DB_PASSWORD=secret

# MongoDB
MONGO_CONNECTION_STRING=mongodb://localhost:27017

# Hangfire (for sync scheduling)
HANGFIRE_CONNECTION_STRING=Host=localhost;Database=aegis;...
```

---

## Security Considerations

1. **Principle of Least Privilege**: Use read-only database accounts
2. **Network Security**: Use VPNs or SSH tunnels for production databases
3. **Credential Rotation**: Regularly update database passwords
4. **Audit Logging**: Monitor sync operations in SyncHistory
5. **Data Sanitization**: Validate and sanitize ingested content

---

## Monitoring

### Sync Metrics

Monitor these key metrics:
- **Success Rate**: `documentsAdded / (documentsAdded + documentsFailed)`
- **Sync Duration**: Track trends over time
- **Document Volume**: Monitor growth rate

### Alerts

Set up alerts for:
- Consecutive sync failures (> 3)
- Unusually long sync duration (> 2x average)
- High failure rate (> 10%)

---

## Next Steps

- [Query History and Feedback](./QUERY_FEATURES.md)
- [Reranking Configuration](./RERANKING.md)
- [API Reference](./API_REFERENCE.md)
