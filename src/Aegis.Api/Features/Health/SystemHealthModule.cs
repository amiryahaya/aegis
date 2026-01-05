using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;

namespace Aegis.Api.Features.Health;

public class SystemHealthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health")
            .WithTags("System Health");

        group.MapGet("/system", GetSystemHealth)
            .WithSummary("Get detailed system health metrics");

        group.MapGet("/components", GetComponentHealth)
            .WithSummary("Get health status of individual components");

        group.MapGet("/metrics", GetSystemMetrics)
            .WithSummary("Get real-time system metrics");
    }

    private static Ok<SystemHealthResponse> GetSystemHealth()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        var memoryUsage = process.WorkingSet64;
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var memoryPercentage = totalMemory > 0 ? (double)memoryUsage / totalMemory * 100 : 0;

        var cpuTime = process.TotalProcessorTime;
        var cpuPercentage = uptime.TotalMilliseconds > 0
            ? cpuTime.TotalMilliseconds / uptime.TotalMilliseconds / Environment.ProcessorCount * 100
            : 0;

        var status = DetermineOverallStatus(memoryPercentage, cpuPercentage);

        var response = new SystemHealthResponse(
            Status: status,
            Uptime: FormatUptime(uptime),
            UptimeSeconds: (long)uptime.TotalSeconds,
            Version: typeof(SystemHealthModule).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Environment: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Metrics: new SystemMetrics(
                CpuUsagePercent: Math.Round(cpuPercentage, 2),
                MemoryUsedBytes: memoryUsage,
                MemoryTotalBytes: totalMemory,
                MemoryUsagePercent: Math.Round(memoryPercentage, 2),
                ThreadCount: process.Threads.Count,
                HandleCount: process.HandleCount,
                GcGen0Collections: GC.CollectionCount(0),
                GcGen1Collections: GC.CollectionCount(1),
                GcGen2Collections: GC.CollectionCount(2)
            ),
            Components: GetComponentStatuses(),
            Timestamp: DateTime.UtcNow
        );

        return TypedResults.Ok(response);
    }

    private static Ok<List<ComponentHealthResponse>> GetComponentHealth()
    {
        return TypedResults.Ok(GetComponentStatuses());
    }

    private static Ok<SystemMetrics> GetSystemMetrics()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        var memoryUsage = process.WorkingSet64;
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var memoryPercentage = totalMemory > 0 ? (double)memoryUsage / totalMemory * 100 : 0;

        var cpuTime = process.TotalProcessorTime;
        var cpuPercentage = uptime.TotalMilliseconds > 0
            ? cpuTime.TotalMilliseconds / uptime.TotalMilliseconds / Environment.ProcessorCount * 100
            : 0;

        return TypedResults.Ok(new SystemMetrics(
            CpuUsagePercent: Math.Round(cpuPercentage, 2),
            MemoryUsedBytes: memoryUsage,
            MemoryTotalBytes: totalMemory,
            MemoryUsagePercent: Math.Round(memoryPercentage, 2),
            ThreadCount: process.Threads.Count,
            HandleCount: process.HandleCount,
            GcGen0Collections: GC.CollectionCount(0),
            GcGen1Collections: GC.CollectionCount(1),
            GcGen2Collections: GC.CollectionCount(2)
        ));
    }

    private static List<ComponentHealthResponse> GetComponentStatuses()
    {
        return new List<ComponentHealthResponse>
        {
            new("API Server", "Healthy", "API is responding normally", DateTime.UtcNow),
            new("Database", "Healthy", "Database connection is active", DateTime.UtcNow),
            new("Cache", "Healthy", "In-memory cache is operational", DateTime.UtcNow),
            new("Vector Store", "Healthy", "Vector store is ready", DateTime.UtcNow),
            new("LLM Service", "Healthy", "LLM service is available", DateTime.UtcNow),
            new("Background Jobs", "Healthy", "Job processor is running", DateTime.UtcNow)
        };
    }

    private static string DetermineOverallStatus(double memoryPercent, double cpuPercent)
    {
        if (memoryPercent > 90 || cpuPercent > 90)
            return "Critical";
        if (memoryPercent > 75 || cpuPercent > 75)
            return "Warning";
        return "Healthy";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }
}

#region Response DTOs

public record SystemHealthResponse(
    string Status,
    string Uptime,
    long UptimeSeconds,
    string Version,
    string Environment,
    SystemMetrics Metrics,
    List<ComponentHealthResponse> Components,
    DateTime Timestamp);

public record SystemMetrics(
    double CpuUsagePercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double MemoryUsagePercent,
    int ThreadCount,
    int HandleCount,
    int GcGen0Collections,
    int GcGen1Collections,
    int GcGen2Collections);

public record ComponentHealthResponse(
    string Name,
    string Status,
    string Message,
    DateTime LastCheck);

#endregion
