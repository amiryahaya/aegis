using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Aegis.Infrastructure.Services.Graph;

public class GraphSchemaService : IGraphSchemaService
{
    private readonly IDriver _driver;
    private readonly ILogger<GraphSchemaService> _logger;

    // Entity types that need schema initialization
    private static readonly string[] EntityLabels =
    {
        "Person", "Organization", "Location", "Malware", "ThreatActor",
        "Vulnerability", "Campaign", "TTP", "Infrastructure", "Tool", "Indicator"
    };

    public GraphSchemaService(IDriver driver, ILogger<GraphSchemaService> logger)
    {
        _driver = driver;
        _logger = logger;
    }

    public async Task<Result> InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                // Create unique constraint on entity ID for each entity type
                foreach (var label in EntityLabels)
                {
                    try
                    {
                        var constraintQuery = $@"
                            CREATE CONSTRAINT {label.ToLower()}_id_unique IF NOT EXISTS
                            FOR (e:{label})
                            REQUIRE e.id IS UNIQUE";
                        await tx.RunAsync(constraintQuery);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create constraint for {Label}", label);
                    }
                }

                // Create indexes on commonly queried properties
                var indexQueries = new[]
                {
                    // Index on name for all entity types
                    "CREATE INDEX entity_name_index IF NOT EXISTS FOR (e:ThreatActor) ON (e.name)",
                    "CREATE INDEX malware_name_index IF NOT EXISTS FOR (e:Malware) ON (e.name)",
                    "CREATE INDEX vulnerability_name_index IF NOT EXISTS FOR (e:Vulnerability) ON (e.name)",
                    "CREATE INDEX campaign_name_index IF NOT EXISTS FOR (e:Campaign) ON (e.name)",

                    // Temporal indexes
                    "CREATE INDEX entity_first_seen_index IF NOT EXISTS FOR (e:ThreatActor) ON (e.firstSeen)",
                    "CREATE INDEX entity_last_seen_index IF NOT EXISTS FOR (e:ThreatActor) ON (e.lastSeen)",

                    // Confidence index for filtering
                    "CREATE INDEX entity_confidence_index IF NOT EXISTS FOR (e:ThreatActor) ON (e.confidence)",
                };

                foreach (var indexQuery in indexQueries)
                {
                    try
                    {
                        await tx.RunAsync(indexQuery);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create index: {Query}", indexQuery);
                    }
                }
            });

            _logger.LogInformation("Graph schema initialized successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize graph schema");
            return Result.Failure(
                Error.Internal("GraphSchema.InitializationFailed", $"Failed to initialize schema: {ex.Message}"));
        }
    }

    public async Task<Result<bool>> VerifySchemaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var hasConstraints = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync("SHOW CONSTRAINTS");
                var constraints = new List<string>();

                await foreach (var record in cursor)
                {
                    var name = record["name"].As<string>();
                    constraints.Add(name);
                }

                // Verify at least some constraints exist
                return constraints.Count > 0;
            });

            var hasIndexes = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync("SHOW INDEXES");
                var indexes = new List<string>();

                await foreach (var record in cursor)
                {
                    var name = record["name"].As<string>();
                    indexes.Add(name);
                }

                // Verify at least some indexes exist
                return indexes.Count > 0;
            });

            var isVerified = hasConstraints && hasIndexes;
            return Result<bool>.Success(isVerified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify graph schema");
            return Result<bool>.Failure(
                Error.Internal("GraphSchema.VerificationFailed", $"Failed to verify schema: {ex.Message}"));
        }
    }
}
