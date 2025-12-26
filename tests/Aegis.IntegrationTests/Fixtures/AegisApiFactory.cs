using System.Net.Http.Json;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Aegis.IntegrationTests.Fixtures;

public class AegisApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string PostgresConnectionString => _postgresContainer.GetConnectionString();
    public string RedisConnectionString => _redisContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing repository registrations
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<ITeamRepository>();
            services.RemoveAll<IWorkspaceRepository>();
            services.RemoveAll<IConversationRepository>();
            services.RemoveAll<IWorkspaceEntityRepository>();
            services.RemoveAll<IWorkspaceFindingRepository>();
            services.RemoveAll<IWorkspaceFactRepository>();
            services.RemoveAll<IDataSourceRepository>();
            services.RemoveAll<IDocumentRepository>();
            services.RemoveAll<IWorkspaceContextService>();

            // Run migrations against test container
            var migrator = new DatabaseMigrator(PostgresConnectionString);
            migrator.Migrate();

            // Register repositories with test container connection string
            services.AddScoped<IUserRepository>(_ => new UserRepository(PostgresConnectionString));
            services.AddScoped<ITeamRepository>(_ => new TeamRepository(PostgresConnectionString));
            services.AddScoped<IWorkspaceRepository>(_ => new WorkspaceRepository(PostgresConnectionString));
            services.AddScoped<IConversationRepository>(_ => new ConversationRepository(PostgresConnectionString));
            services.AddScoped<IWorkspaceEntityRepository>(_ => new WorkspaceEntityRepository(PostgresConnectionString));
            services.AddScoped<IWorkspaceFindingRepository>(_ => new WorkspaceFindingRepository(PostgresConnectionString));
            services.AddScoped<IWorkspaceFactRepository>(_ => new WorkspaceFactRepository(PostgresConnectionString));
            services.AddScoped<IDataSourceRepository>(_ => new DataSourceRepository(PostgresConnectionString));
            services.AddScoped<IDocumentRepository>(_ => new DocumentRepository(PostgresConnectionString));
            services.AddScoped<IWorkspaceContextService, Aegis.Infrastructure.Services.Workspace.WorkspaceContextService>();

            // Register RAG services for testing
            services.RemoveAll<IDocumentParser>();
            services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.PdfDocumentParser>();
            services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.DocxDocumentParser>();

            services.RemoveAll<ITextChunker>();
            services.AddScoped<ITextChunker, Aegis.Infrastructure.Services.Chunking.TextChunker>();

            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService>(_ => new Aegis.Infrastructure.Services.Embedding.InMemoryEmbeddingService());

            services.RemoveAll<IVectorStore>();
            services.AddSingleton<IVectorStore, Aegis.Infrastructure.Services.VectorStore.InMemoryVectorStore>();

            services.RemoveAll<IBM25Indexer>();
            services.AddSingleton<IBM25Indexer, Aegis.Infrastructure.Services.BM25.InMemoryBM25Indexer>();

            services.RemoveAll<IHybridRetriever>();
            services.AddScoped<IHybridRetriever, Aegis.Infrastructure.Services.Retrieval.HybridRetriever>();

            services.RemoveAll<ILLMService>();
            services.AddScoped<ILLMService, Aegis.Infrastructure.Services.LLM.MockLLMService>();

            // Register RAG query services
            services.RemoveAll<IQueryProcessor>();
            services.AddScoped<IQueryProcessor, Aegis.Infrastructure.Services.Query.QueryProcessor>();

            services.RemoveAll<IRAGContextAssembler>();
            services.AddScoped<IRAGContextAssembler, Aegis.Infrastructure.Services.Query.RAGContextAssembler>();

            services.RemoveAll<IRAGQueryService>();
            services.AddScoped<IRAGQueryService, Aegis.Infrastructure.Services.Query.RAGQueryService>();

            // Remove Neo4j services for testing (they cause initialization issues)
            services.RemoveAll<IGraphService>();
            services.RemoveAll<IGraphSchemaService>();
            services.RemoveAll<IEntityIngestionService>();
            services.RemoveAll<IRelationshipExtractionService>();
            services.RemoveAll<IGraphQueryService>();
            services.RemoveAll<IGraphEnhancedRetriever>();
        });

        builder.UseEnvironment("Testing");
        builder.UseSetting("Neo4j:Uri", ""); // Disable Neo4j
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        // Recreate database schema
        var migrator = new DatabaseMigrator(PostgresConnectionString);
        await Task.Run(() => migrator.Migrate());
    }

    public async Task<(Guid userId, Guid workspaceId, Guid teamId, string token)> CreateAuthenticatedUserWithTeamAsync(
        HttpClient client)
    {
        // Register user
        var email = $"test-{Guid.NewGuid()}@example.com";
        var password = "Test123!";

        var registerPayload = new
        {
            email = email,
            name = "Test User",
            password = password
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerPayload);
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        var registerJson = System.Text.Json.JsonDocument.Parse(registerContent);
        var userId = Guid.Parse(registerJson.RootElement.GetProperty("id").GetString()!);

        // Login to get token and workspaceId
        var loginPayload = new
        {
            email = email,
            password = password
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginJson = System.Text.Json.JsonDocument.Parse(loginContent);
        var token = loginJson.RootElement.GetProperty("token").GetString()!;

        // Create a workspace
        var workspacePayload = new
        {
            name = "Test Workspace",
            description = "Test workspace for integration tests",
            createdBy = userId
        };

        var workspaceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces")
        {
            Content = System.Net.Http.Json.JsonContent.Create(workspacePayload),
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) }
        };

        var workspaceResponse = await client.SendAsync(workspaceRequest);
        var workspaceContent = await workspaceResponse.Content.ReadAsStringAsync();

        if (!workspaceResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Workspace creation failed with status {workspaceResponse.StatusCode}. Response: {workspaceContent}");
        }
        var workspaceJson = System.Text.Json.JsonDocument.Parse(workspaceContent);
        var workspaceId = workspaceJson.RootElement.TryGetProperty("id", out var idProp)
            ? Guid.Parse(idProp.GetString()!)
            : Guid.Parse(workspaceJson.RootElement.GetProperty("Id").GetString()!);

        // Create team with unique name
        var teamPayload = new
        {
            name = $"Test Team {Guid.NewGuid()}",
            ownerId = userId,
            description = "Test team for integration tests"
        };

        var teamRequest = new HttpRequestMessage(HttpMethod.Post, "/api/teams")
        {
            Content = System.Net.Http.Json.JsonContent.Create(teamPayload),
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) }
        };

        var teamResponse = await client.SendAsync(teamRequest);
        var teamContent = await teamResponse.Content.ReadAsStringAsync();

        if (!teamResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Team creation failed with status {teamResponse.StatusCode}. Response: {teamContent}");
        }
        var teamJson = System.Text.Json.JsonDocument.Parse(teamContent);
        var teamId = teamJson.RootElement.TryGetProperty("id", out var teamIdProp)
            ? Guid.Parse(teamIdProp.GetString()!)
            : Guid.Parse(teamJson.RootElement.GetProperty("Id").GetString()!);

        return (userId, workspaceId, teamId, token);
    }
}

[CollectionDefinition("Database")]
public class DatabaseTestCollection : ICollectionFixture<AegisApiFactory>
{
}

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<AegisApiFactory>
{
}
