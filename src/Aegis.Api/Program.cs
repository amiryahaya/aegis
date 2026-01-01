using Aegis.Api.Extensions;
using Aegis.Api.Hubs;
using Carter;
using Hangfire;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AEGIS API");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .WriteTo.Console());

    // Add services
    builder.Services
        .AddApplicationServices()
        .AddInfrastructureServices(builder.Configuration)
        .AddApiServices(builder.Configuration)
        .AddObservability(builder.Configuration);

    // Configure JSON serialization
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

    var app = builder.Build();

    // Configure middleware pipeline
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseHangfireDashboard("/hangfire");

        // Enable Swagger in development
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AEGIS RAG API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "AEGIS RAG API Documentation";
            options.EnableDeepLinking();
            options.EnableFilter();
            options.EnableTryItOutByDefault();
        });
    }

    // Observability (metrics, tracing)
    app.UseObservability();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapDetailedHealthChecks();
    app.MapCarter();

    // Map SignalR hubs
    app.MapHub<QueryHub>("/hubs/query");

    // Map a simple root endpoint
    app.MapGet("/", () => Results.Ok(new
    {
        name = "AEGIS API",
        version = "1.0.0",
        status = "running",
        documentation = app.Environment.IsDevelopment() ? "/swagger" : null,
        health = "/health",
        metrics = "/metrics"
    }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for integration tests
public partial class Program { }
