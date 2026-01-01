using System.Reflection;
using System.Text;
using Aegis.Api.Middleware;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Aegis.Infrastructure.Services;
using Aegis.Infrastructure.Services.Graph;
using Aegis.Infrastructure.Services.Sync;
using Aegis.Infrastructure.Services.Connectors;
using Aegis.Infrastructure.Services.Reranking;
using Aegis.Infrastructure.Services.Search;
using Aegis.Infrastructure.Services.Agents;
using Aegis.Api.Features.Agents;
using Carter;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Aegis.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5434;Database=aegis;Username=postgres;Password=postgres";

        // Run database migrations
        var migrator = new DatabaseMigrator(connectionString);
        var result = migrator.Migrate();
        if (!result.Successful)
        {
            throw new Exception($"Database migration failed: {result.Error?.Message}");
        }

        // Register repositories
        services.AddScoped<IUserRepository>(_ => new UserRepository(connectionString));
        services.AddScoped<ITeamRepository>(_ => new TeamRepository(connectionString));
        services.AddScoped<IWorkspaceRepository>(_ => new WorkspaceRepository(connectionString));
        services.AddScoped<IConversationRepository>(_ => new ConversationRepository(connectionString));
        services.AddScoped<IWorkspaceEntityRepository>(_ => new WorkspaceEntityRepository(connectionString));
        services.AddScoped<IWorkspaceFindingRepository>(_ => new WorkspaceFindingRepository(connectionString));
        services.AddScoped<IWorkspaceFactRepository>(_ => new WorkspaceFactRepository(connectionString));
        services.AddScoped<IDataSourceRepository>(_ => new DataSourceRepository(connectionString));
        services.AddScoped<IDocumentRepository>(_ => new DocumentRepository(connectionString));
        services.AddScoped<ISyncHistoryRepository>(_ => new SyncHistoryRepository(connectionString));
        services.AddScoped<IQueryHistoryRepository>(_ => new QueryHistoryRepository(connectionString));
        services.AddScoped<IFeedbackRepository>(_ => new FeedbackRepository(connectionString));

        // Configure Hangfire for background jobs
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        // Register sync services
        services.AddScoped<DataSourceSyncJob>();
        services.AddScoped<ISyncScheduler, HangfireSyncScheduler>();

        // Register data connectors
        services.AddScoped<PostgreSqlConnector>();
        services.AddScoped<MongoDbConnector>();
        services.AddScoped<RssFeedConnector>();
        services.AddHttpClient(); // For RSS feed connector
        services.AddSingleton<IDataConnectorFactory>(sp =>
        {
            var factory = new DataConnectorFactory(sp);
            factory.RegisterConnector(Aegis.Domain.Entities.DataSourceType.PostgreSQL, typeof(PostgreSqlConnector));
            factory.RegisterConnector(Aegis.Domain.Entities.DataSourceType.MongoDB, typeof(MongoDbConnector));
            factory.RegisterConnector(Aegis.Domain.Entities.DataSourceType.RssFeed, typeof(RssFeedConnector));
            return factory;
        });

        // Register domain services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IWorkspaceContextService, Aegis.Infrastructure.Services.Workspace.WorkspaceContextService>();
        services.AddScoped<IQueryProcessor, Aegis.Infrastructure.Services.Query.QueryProcessor>();
        services.AddScoped<IRAGContextAssembler, Aegis.Infrastructure.Services.Query.RAGContextAssembler>();
        services.AddScoped<IRAGQueryService, Aegis.Infrastructure.Services.Query.RAGQueryService>();

        // Register RAG services
        services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.PdfDocumentParser>();
        services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.DocxDocumentParser>();
        services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.PptxDocumentParser>();
        services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.SpreadsheetParser>();
        services.AddScoped<IDocumentParser, Aegis.Infrastructure.Services.DocumentParsing.HtmlDocumentParser>();

        // Register chunking options with default values
        services.AddSingleton(new ChunkingOptions
        {
            MaxChunkSize = 512,
            ChunkOverlap = 50,
            Strategy = ChunkingStrategy.Sentence
        });
        services.AddScoped<ITextChunker, Aegis.Infrastructure.Services.Chunking.TextChunker>();
        services.AddSingleton<IEmbeddingService>(_ => new Aegis.Infrastructure.Services.Embedding.InMemoryEmbeddingService());
        services.AddSingleton<IVectorStore, Aegis.Infrastructure.Services.VectorStore.InMemoryVectorStore>();
        services.AddSingleton<IBM25Indexer, Aegis.Infrastructure.Services.BM25.InMemoryBM25Indexer>();
        services.AddScoped<IHybridRetriever, Aegis.Infrastructure.Services.Retrieval.HybridRetriever>();

        // Register LLM Service (OpenAI if API key provided, otherwise Mock)
        var openAIKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAIKey))
        {
            services.AddScoped<ILLMService>(sp => new Aegis.Infrastructure.Services.LLM.OpenAILLMService(
                openAIKey,
                sp.GetRequiredService<ILogger<Aegis.Infrastructure.Services.LLM.OpenAILLMService>>(),
                configuration["OpenAI:Model"] ?? "gpt-4o-mini"));
        }
        else
        {
            services.AddScoped<ILLMService, Aegis.Infrastructure.Services.LLM.MockLLMService>();
        }

        // Register Reranker Service (Cohere if API key provided, otherwise Simple fallback)
        var cohereApiKey = configuration["Cohere:ApiKey"] ?? Environment.GetEnvironmentVariable("COHERE_API_KEY");
        if (!string.IsNullOrWhiteSpace(cohereApiKey))
        {
            services.AddScoped<IRerankerService>(sp => new CohereRerankerService(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<CohereRerankerService>>(),
                cohereApiKey,
                configuration["Cohere:Model"] ?? "rerank-english-v3.0"));
        }
        else
        {
            services.AddScoped<IRerankerService, SimpleRerankerService>();
        }

        // Register search services
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddScoped<IKeywordSearchService, KeywordSearchService>();

        // Register plugin authorization service
        services.AddScoped<IPluginAuthorizationService, PluginAuthorizationService>();

        // Register Semantic Kernel service with all plugins
        services.AddScoped<SemanticKernelService>();

        // Register agent orchestration services
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddScoped<IEvaluatorAgent, EvaluatorAgent>();
        services.AddScoped<IAgent, RetrieverAgent>(sp => new RetrieverAgent(
            sp.GetRequiredService<ISemanticSearchService>(),
            sp.GetRequiredService<IKeywordSearchService>(),
            sp.GetRequiredService<ILogger<RetrieverAgent>>()));
        services.AddScoped<IAgent, AnalyzerAgent>(sp => new AnalyzerAgent(
            sp.GetRequiredService<ILLMService>(),
            sp.GetRequiredService<ILogger<AnalyzerAgent>>()));
        services.AddScoped<IAgent, SynthesizerAgent>(sp => new SynthesizerAgent(
            sp.GetRequiredService<ILLMService>(),
            sp.GetRequiredService<ILogger<SynthesizerAgent>>()));
        services.AddScoped<IAgent, EvaluatorAgent>(sp => new EvaluatorAgent(
            sp.GetRequiredService<ILLMService>(),
            sp.GetRequiredService<ILogger<EvaluatorAgent>>()));

        // Register working memory service
        services.AddSingleton<IWorkingMemory, WorkingMemoryService>();

        // Register refinement loop for iterative response improvement
        services.AddScoped<IRefinementLoop, RefinementLoop>();

        // Register reasoning trace logger (singleton to persist traces across requests)
        services.AddSingleton<IReasoningTraceLogger, ReasoningTraceLogger>();

        // Register follow-up question generator
        services.AddScoped<IFollowUpGenerator, FollowUpGenerator>();

        // Register task executor with agent dictionary
        services.AddScoped<ITaskExecutor>(sp =>
        {
            var agents = new Dictionary<string, IAgent>
            {
                { "Planner", sp.GetRequiredService<IPlannerAgent>() as IAgent },
                { "Retriever", sp.GetServices<IAgent>().First(a => a.AgentType == "Retriever") },
                { "Analyzer", sp.GetServices<IAgent>().First(a => a.AgentType == "Analyzer") },
                { "Synthesizer", sp.GetServices<IAgent>().First(a => a.AgentType == "Synthesizer") },
                { "Evaluator", sp.GetRequiredService<IEvaluatorAgent>() as IAgent }
            };

            return new TaskExecutor(
                agents!,
                sp.GetRequiredService<ILogger<TaskExecutor>>());
        });

        services.AddScoped<ITableExtractor, Aegis.Infrastructure.Services.Tables.HtmlTableExtractor>();

        // Register NER services (Intelligence NER wraps Basic NER)
        services.AddScoped<Aegis.Infrastructure.Services.NER.BasicNERService>();
        services.AddScoped<INERService, Aegis.Infrastructure.Services.NER.IntelligenceNERService>(sp =>
            new Aegis.Infrastructure.Services.NER.IntelligenceNERService(
                sp.GetRequiredService<Aegis.Infrastructure.Services.NER.BasicNERService>(),
                sp.GetRequiredService<ILogger<Aegis.Infrastructure.Services.NER.IntelligenceNERService>>()));

        // Register sentiment analysis
        services.AddScoped<ISentimentAnalyzer, Aegis.Infrastructure.Services.Sentiment.LexiconSentimentAnalyzer>();

        // Register language detection
        services.AddScoped<ILanguageDetector, Aegis.Infrastructure.Services.Language.PatternLanguageDetector>();

        // Register OCR service
        var tessDataPath = configuration["OCR:TessDataPath"]
                          ?? Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
                          ?? Path.Combine(AppContext.BaseDirectory, "tessdata");

        // Only register if tessdata exists
        if (Directory.Exists(tessDataPath))
        {
            services.AddSingleton<IOCRService>(sp =>
                new Aegis.Infrastructure.Services.OCR.TesseractOCRService(
                    tessDataPath,
                    sp.GetRequiredService<ILogger<Aegis.Infrastructure.Services.OCR.TesseractOCRService>>()));
        }

        // Register Neo4j Graph Service
        var neo4jUri = configuration["Neo4j:Uri"] ?? Environment.GetEnvironmentVariable("NEO4J_URI");
        var neo4jUsername = configuration["Neo4j:Username"] ?? Environment.GetEnvironmentVariable("NEO4J_USERNAME");
        var neo4jPassword = configuration["Neo4j:Password"] ?? Environment.GetEnvironmentVariable("NEO4J_PASSWORD");

        // Only register Neo4j if all credentials are provided and URI is not empty string
        if (!string.IsNullOrWhiteSpace(neo4jUri) && !string.IsNullOrEmpty(neo4jUsername) && !string.IsNullOrEmpty(neo4jPassword))
        {
            services.AddSingleton(sp =>
            {
                return Neo4j.Driver.GraphDatabase.Driver(
                    neo4jUri,
                    Neo4j.Driver.AuthTokens.Basic(neo4jUsername, neo4jPassword),
                    o => o
                        .WithConnectionTimeout(TimeSpan.FromSeconds(5))
                        .WithMaxConnectionLifetime(TimeSpan.FromMinutes(10)));
            });

            services.AddScoped<IGraphService, Aegis.Infrastructure.Services.Graph.Neo4jService>();
            services.AddScoped<IGraphSchemaService, Aegis.Infrastructure.Services.Graph.GraphSchemaService>();
            services.AddScoped<IEntityIngestionService, Aegis.Infrastructure.Services.Graph.EntityIngestionService>();
            services.AddScoped<IRelationshipExtractionService, Aegis.Infrastructure.Services.Graph.RelationshipExtractionService>();
            services.AddScoped<IGraphQueryService, Aegis.Infrastructure.Services.Graph.GraphQueryService>();
            services.AddScoped<IGraphEnhancedRetriever, Aegis.Infrastructure.Services.Graph.GraphEnhancedRetriever>();

            // Note: Schema initialization moved to Program.cs to avoid service provider disposal issues
        }

        // Add health checks
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql")
            .AddRedis(configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Carter for minimal API modules
        services.AddCarter();

        // Exception handling
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Problem Details
        services.AddProblemDetails();

        // CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        // JWT Authentication
        var jwtSecret = configuration["Jwt:Secret"] ?? "your-super-secret-key-minimum-32-characters-long!";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "aegis-api";
        var jwtAudience = configuration["Jwt:Audience"] ?? "aegis-client";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
        });

        services.AddAuthorization();

        // SignalR
        services.AddSignalR();

        return services;
    }
}

/// <summary>
/// MediatR behavior for validation
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}

/// <summary>
/// MediatR behavior for logging
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next();

        _logger.LogInformation("Handled {RequestName}", requestName);

        return response;
    }
}
