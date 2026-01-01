using Asp.Versioning;

namespace Aegis.Api.Extensions;

/// <summary>
/// Extension methods for configuring API versioning
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning services with default configuration
    /// </summary>
    public static IServiceCollection AddApiVersioningServices(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            // Default version when not specified
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;

            // Report available versions in response headers
            options.ReportApiVersions = true;

            // Support multiple version readers (header, query string, URL segment)
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"),
                new QueryStringApiVersionReader("api-version"));
        });

        return services;
    }
}

/// <summary>
/// API version constants
/// </summary>
public static class ApiVersions
{
    public const string V1 = "1.0";
    public const string V2 = "2.0";

    /// <summary>
    /// Route prefix for versioned APIs
    /// </summary>
    public const string RoutePrefix = "api/v{version:apiVersion}";
}
