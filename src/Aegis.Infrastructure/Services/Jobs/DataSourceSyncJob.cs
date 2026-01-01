using System.Diagnostics;
using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Jobs;

/// <summary>
/// Background job for syncing data sources
/// </summary>
public class DataSourceSyncJob : IDataSourceSyncJob
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ILogger<DataSourceSyncJob> _logger;

    public DataSourceSyncJob(
        IDataSourceRepository dataSourceRepository,
        IDocumentRepository documentRepository,
        IBackgroundJobService backgroundJobService,
        ILogger<DataSourceSyncJob> logger)
    {
        _dataSourceRepository = dataSourceRepository ?? throw new ArgumentNullException(nameof(dataSourceRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _backgroundJobService = backgroundJobService ?? throw new ArgumentNullException(nameof(backgroundJobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [120, 600, 1800])]
    [Queue("default")]
    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data source sync job for {DataSourceId}", jobId);

        var result = await SyncDataSourceAsync(jobId, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Data source sync failed for {DataSourceId}: {Error}",
                jobId, result.Error?.Message);
            throw new InvalidOperationException($"Data source sync failed: {result.Error?.Message}");
        }

        _logger.LogInformation("Data source sync completed for {DataSourceId}: {Added} added, {Updated} updated, {Removed} removed in {SyncTime}",
            jobId, result.Value.DocumentsAdded, result.Value.DocumentsUpdated,
            result.Value.DocumentsRemoved, result.Value.SyncTime);
    }

    public async Task<Result<DataSourceSyncResult>> SyncDataSourceAsync(
        Guid dataSourceId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId, cancellationToken);
            if (dataSource == null)
            {
                return Result<DataSourceSyncResult>.Failure(
                    Error.NotFound("DataSource.NotFound", $"Data source {dataSourceId} not found"));
            }

            int documentsAdded = 0;
            int documentsUpdated = 0;
            int documentsRemoved = 0;

            // Sync logic depends on data source type
            switch (dataSource.Type)
            {
                case DataSourceType.RestApi:
                    var apiResult = await SyncRestApiSourceAsync(dataSource, cancellationToken);
                    documentsAdded = apiResult.Added;
                    documentsUpdated = apiResult.Updated;
                    break;

                case DataSourceType.RssFeed:
                    var rssResult = await SyncRssFeedSourceAsync(dataSource, cancellationToken);
                    documentsAdded = rssResult.Added;
                    documentsUpdated = rssResult.Updated;
                    break;

                case DataSourceType.PostgreSQL:
                case DataSourceType.MySQL:
                case DataSourceType.MSSQL:
                case DataSourceType.MongoDB:
                    var dbResult = await SyncDatabaseSourceAsync(dataSource, cancellationToken);
                    documentsAdded = dbResult.Added;
                    documentsUpdated = dbResult.Updated;
                    documentsRemoved = dbResult.Removed;
                    break;

                case DataSourceType.WebScraper:
                    var webResult = await SyncWebScraperSourceAsync(dataSource, cancellationToken);
                    documentsAdded = webResult.Added;
                    documentsUpdated = webResult.Updated;
                    break;

                default:
                    _logger.LogWarning("Unsupported data source type for sync: {Type}", dataSource.Type);
                    break;
            }

            // Queue document processing for new/updated documents
            var documentsToProcess = await _documentRepository.GetPendingProcessingAsync(
                dataSourceId, cancellationToken);

            foreach (var doc in documentsToProcess)
            {
                _backgroundJobService.Enqueue<IDocumentProcessingJob>(doc.Id);
            }

            stopwatch.Stop();

            // Calculate next sync time (e.g., based on sync interval in config)
            var nextSyncAt = DateTime.UtcNow.AddHours(1);

            return Result<DataSourceSyncResult>.Success(new DataSourceSyncResult(
                dataSourceId,
                documentsAdded,
                documentsUpdated,
                documentsRemoved,
                stopwatch.Elapsed,
                nextSyncAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data source {DataSourceId}", dataSourceId);
            stopwatch.Stop();

            return Result<DataSourceSyncResult>.Success(new DataSourceSyncResult(
                dataSourceId,
                0,
                0,
                0,
                stopwatch.Elapsed,
                null,
                ex.Message));
        }
    }

    private async Task<(int Added, int Updated)> SyncRestApiSourceAsync(
        DataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Implementation would fetch data from REST API and create/update documents
        _logger.LogDebug("Syncing REST API source: {DataSourceId}", dataSource.Id);
        await Task.Delay(100, cancellationToken); // Simulate API call
        return (0, 0);
    }

    private async Task<(int Added, int Updated)> SyncRssFeedSourceAsync(
        DataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Implementation would fetch RSS feed and create/update documents
        _logger.LogDebug("Syncing RSS feed source: {DataSourceId}", dataSource.Id);
        await Task.Delay(100, cancellationToken); // Simulate feed fetch
        return (0, 0);
    }

    private async Task<(int Added, int Updated, int Removed)> SyncDatabaseSourceAsync(
        DataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Implementation would query database and sync documents
        _logger.LogDebug("Syncing database source: {DataSourceId}", dataSource.Id);
        await Task.Delay(100, cancellationToken); // Simulate DB query
        return (0, 0, 0);
    }

    private async Task<(int Added, int Updated)> SyncWebScraperSourceAsync(
        DataSource dataSource,
        CancellationToken cancellationToken)
    {
        // Implementation would scrape web pages and create/update documents
        _logger.LogDebug("Syncing web scraper source: {DataSourceId}", dataSource.Id);
        await Task.Delay(100, cancellationToken); // Simulate scraping
        return (0, 0);
    }
}
