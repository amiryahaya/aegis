using Aegis.Infrastructure.Services.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

namespace Aegis.Api.Extensions;

/// <summary>
/// Extension methods for configuring Hangfire background job processing
/// </summary>
public static class HangfireExtensions
{
    /// <summary>
    /// Adds Hangfire services with PostgreSQL storage
    /// </summary>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string is required for Hangfire");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions
            {
                SchemaName = "hangfire",
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                PrepareSchemaIfNecessary = true,
                TransactionSynchronisationTimeout = TimeSpan.FromMinutes(5),
                InvisibilityTimeout = TimeSpan.FromMinutes(30)
            }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
            options.Queues = ["critical", "default", "low"];
            options.ServerName = $"aegis-{Environment.MachineName}";
        });

        return services;
    }

    /// <summary>
    /// Maps Hangfire dashboard and endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapHangfireEndpoints(
        this IEndpointRouteBuilder endpoints,
        IConfiguration configuration)
    {
        var dashboardPath = configuration.GetValue("Hangfire:DashboardPath", "/hangfire");

        // Dashboard with basic authorization (should be enhanced in production)
        endpoints.MapHangfireDashboard(dashboardPath, new DashboardOptions
        {
            DashboardTitle = "AEGIS Background Jobs",
            DisplayStorageConnectionString = false,
            Authorization = [new HangfireDashboardAuthorizationFilter()]
        });

        return endpoints;
    }

    /// <summary>
    /// Configures recurring jobs for the application
    /// </summary>
    public static void ConfigureRecurringJobs(this IApplicationBuilder app)
    {
        var manager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();

        // Cleanup job - runs daily at 2 AM
        manager.AddOrUpdate<ICleanupJobRunner>(
            "cleanup-expired-data",
            runner => runner.ExecuteAsync(CancellationToken.None),
            Cron.Daily(2),
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc,
                MisfireHandling = MisfireHandlingMode.Ignorable
            });

        // Data source sync check - runs every hour
        manager.AddOrUpdate<IDataSourceSyncRunner>(
            "check-datasource-sync",
            runner => runner.CheckAndSyncDataSourcesAsync(CancellationToken.None),
            Cron.Hourly,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc,
                MisfireHandling = MisfireHandlingMode.Relaxed
            });
    }
}

/// <summary>
/// Basic authorization filter for Hangfire dashboard
/// In production, this should validate against your auth system
/// </summary>
public class HangfireDashboardAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In development, allow all access
        var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            return true;
        }

        // In production, require authentication
        return httpContext.User?.Identity?.IsAuthenticated ?? false;
    }
}

