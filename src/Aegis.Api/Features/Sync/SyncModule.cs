using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Sync;

public class SyncModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sync")
            .WithTags("Sync")
            .RequireAuthorization();

        group.MapPost("/test-connection", TestConnection)
            .WithName("TestConnection")
            .WithSummary("Test connection to a data source");

        group.MapPost("/trigger/{dataSourceId}", TriggerSync)
            .WithName("TriggerSync")
            .WithSummary("Trigger an immediate sync for a data source");

        group.MapPost("/schedule/{dataSourceId}", ScheduleSync)
            .WithName("ScheduleSync")
            .WithSummary("Schedule a recurring sync for a data source");

        group.MapDelete("/schedule/{dataSourceId}", CancelScheduledSync)
            .WithName("CancelScheduledSync")
            .WithSummary("Cancel a scheduled sync for a data source");

        group.MapGet("/history/{dataSourceId}", GetSyncHistory)
            .WithName("GetSyncHistory")
            .WithSummary("Get sync history for a data source");

        group.MapGet("/status/{jobId}", GetSyncJobStatus)
            .WithName("GetSyncJobStatus")
            .WithSummary("Get status of a sync job");
    }

    private static async Task<IResult> TestConnection(
        [FromBody] TestConnectionRequest request,
        [FromServices] IDataSourceRepository dataSourceRepository,
        [FromServices] IDataConnectorFactory connectorFactory,
        CancellationToken cancellationToken)
    {
        // Get data source
        var dataSource = await dataSourceRepository.GetByIdAsync(request.DataSourceId, cancellationToken);
        if (dataSource == null)
        {
            return Results.NotFound(new { error = "Data source not found" });
        }

        // Get connector
        var connectorResult = connectorFactory.GetConnector(dataSource.Type);
        if (connectorResult.IsFailure)
        {
            return Results.BadRequest(new { error = connectorResult.Error!.Message });
        }

        // Test connection
        var testResult = await connectorResult.Value.TestConnectionAsync(dataSource.Settings, cancellationToken);
        if (testResult.IsFailure)
        {
            return Results.BadRequest(new { error = testResult.Error!.Message });
        }

        return Results.Ok(new
        {
            success = testResult.Value.IsSuccessful,
            message = testResult.Value.Message,
            metadata = testResult.Value.Metadata
        });
    }

    private static async Task<IResult> TriggerSync(
        Guid dataSourceId,
        [FromServices] IDataSourceRepository dataSourceRepository,
        [FromServices] ISyncScheduler syncScheduler,
        CancellationToken cancellationToken)
    {
        // Verify data source exists
        var dataSource = await dataSourceRepository.GetByIdAsync(dataSourceId, cancellationToken);
        if (dataSource == null)
        {
            return Results.NotFound(new { error = "Data source not found" });
        }

        // Trigger sync
        var result = await syncScheduler.TriggerImmediateSyncAsync(dataSourceId, cancellationToken);
        if (result.IsFailure)
        {
            return Results.BadRequest(new { error = result.Error!.Message });
        }

        return Results.Ok(new
        {
            jobId = result.Value,
            message = "Sync triggered successfully"
        });
    }

    private static async Task<IResult> ScheduleSync(
        Guid dataSourceId,
        [FromBody] ScheduleSyncRequest request,
        [FromServices] IDataSourceRepository dataSourceRepository,
        [FromServices] ISyncScheduler syncScheduler,
        CancellationToken cancellationToken)
    {
        // Verify data source exists
        var dataSource = await dataSourceRepository.GetByIdAsync(dataSourceId, cancellationToken);
        if (dataSource == null)
        {
            return Results.NotFound(new { error = "Data source not found" });
        }

        // Schedule sync
        var result = await syncScheduler.ScheduleRecurringSyncAsync(dataSourceId, request.CronExpression, cancellationToken);
        if (result.IsFailure)
        {
            return Results.BadRequest(new { error = result.Error!.Message });
        }

        return Results.Ok(new
        {
            jobId = result.Value,
            cronExpression = request.CronExpression,
            message = "Sync scheduled successfully"
        });
    }

    private static async Task<IResult> CancelScheduledSync(
        Guid dataSourceId,
        [FromServices] IDataSourceRepository dataSourceRepository,
        [FromServices] ISyncScheduler syncScheduler,
        CancellationToken cancellationToken)
    {
        // Verify data source exists
        var dataSource = await dataSourceRepository.GetByIdAsync(dataSourceId, cancellationToken);
        if (dataSource == null)
        {
            return Results.NotFound(new { error = "Data source not found" });
        }

        // Cancel scheduled sync
        var result = await syncScheduler.CancelScheduledSyncAsync(dataSourceId, cancellationToken);
        if (result.IsFailure)
        {
            return Results.BadRequest(new { error = result.Error!.Message });
        }

        return Results.Ok(new { message = "Scheduled sync cancelled successfully" });
    }

    private static async Task<IResult> GetSyncHistory(
        Guid dataSourceId,
        [FromQuery] int limit,
        [FromServices] IDataSourceRepository dataSourceRepository,
        [FromServices] ISyncHistoryRepository syncHistoryRepository,
        CancellationToken cancellationToken)
    {
        // Verify data source exists
        var dataSource = await dataSourceRepository.GetByIdAsync(dataSourceId, cancellationToken);
        if (dataSource == null)
        {
            return Results.NotFound(new { error = "Data source not found" });
        }

        // Get sync history
        var history = await syncHistoryRepository.GetByDataSourceIdAsync(dataSourceId, limit > 0 ? limit : 50, cancellationToken);

        return Results.Ok(history.Select(h => new
        {
            h.Id,
            h.DataSourceId,
            Type = h.Type.ToString(),
            Status = h.Status.ToString(),
            h.StartedAt,
            h.CompletedAt,
            h.DocumentsAdded,
            h.DocumentsUpdated,
            h.DocumentsDeleted,
            h.DocumentsFailed,
            h.ErrorMessage,
            h.Metadata,
            h.LastSyncCursor,
            h.CreatedAt
        }));
    }

    private static async Task<IResult> GetSyncJobStatus(
        string jobId,
        [FromServices] ISyncScheduler syncScheduler,
        CancellationToken cancellationToken)
    {
        var result = await syncScheduler.GetSyncJobStatusAsync(jobId, cancellationToken);
        if (result.IsFailure)
        {
            return Results.BadRequest(new { error = result.Error!.Message });
        }

        return Results.Ok(new
        {
            result.Value.JobId,
            result.Value.Status,
            result.Value.NextRunTime,
            result.Value.LastRunTime
        });
    }
}

public record TestConnectionRequest(Guid DataSourceId);
public record ScheduleSyncRequest(string CronExpression);
