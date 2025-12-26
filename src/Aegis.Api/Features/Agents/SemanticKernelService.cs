using Aegis.Api.Features.Agents.Plugins;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Features.Agents;

/// <summary>
/// Service for managing Semantic Kernel with registered plugins
/// </summary>
public class SemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelService> _logger;

    public SemanticKernelService(
        ISemanticSearchService semanticSearchService,
        IKeywordSearchService keywordSearchService,
        IGraphService graphService,
        IGraphQueryService graphQueryService,
        ILoggerFactory loggerFactory,
        ILogger<SemanticKernelService> logger)
    {
        _logger = logger;

        // Create kernel builder
        var builder = Kernel.CreateBuilder();

        // Build kernel
        _kernel = builder.Build();

        // Register all plugins
        RegisterPlugins(
            semanticSearchService,
            keywordSearchService,
            graphService,
            graphQueryService,
            loggerFactory);

        _logger.LogInformation("Semantic Kernel initialized with {PluginCount} plugins", _kernel.Plugins.Count);
    }

    private void RegisterPlugins(
        ISemanticSearchService semanticSearchService,
        IKeywordSearchService keywordSearchService,
        IGraphService graphService,
        IGraphQueryService graphQueryService,
        ILoggerFactory loggerFactory)
    {
        try
        {
            // Register search plugins
            var vectorSearchPlugin = new VectorSearchPlugin(
                semanticSearchService,
                loggerFactory.CreateLogger<VectorSearchPlugin>());
            _kernel.Plugins.AddFromObject(vectorSearchPlugin, "VectorSearchPlugin");

            var keywordSearchPlugin = new KeywordSearchPlugin(
                keywordSearchService,
                loggerFactory.CreateLogger<KeywordSearchPlugin>());
            _kernel.Plugins.AddFromObject(keywordSearchPlugin, "KeywordSearchPlugin");

            // Register graph plugins
            var graphQueryPlugin = new GraphQueryPlugin(
                graphService,
                loggerFactory.CreateLogger<GraphQueryPlugin>());
            _kernel.Plugins.AddFromObject(graphQueryPlugin, "GraphQueryPlugin");

            var entityLookupPlugin = new EntityLookupPlugin(
                graphService,
                loggerFactory.CreateLogger<EntityLookupPlugin>());
            _kernel.Plugins.AddFromObject(entityLookupPlugin, "EntityLookupPlugin");

            // Register analysis plugins
            var sanctionsCheckPlugin = new SanctionsCheckPlugin(
                loggerFactory.CreateLogger<SanctionsCheckPlugin>());
            _kernel.Plugins.AddFromObject(sanctionsCheckPlugin, "SanctionsCheckPlugin");

            var timelineBuilderPlugin = new TimelineBuilderPlugin(
                graphQueryService,
                loggerFactory.CreateLogger<TimelineBuilderPlugin>());
            _kernel.Plugins.AddFromObject(timelineBuilderPlugin, "TimelineBuilderPlugin");

            _logger.LogInformation("Successfully registered 6 plugins with Semantic Kernel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering plugins with Semantic Kernel");
            throw;
        }
    }

    /// <summary>
    /// Gets the configured Semantic Kernel instance
    /// </summary>
    public Kernel GetKernel() => _kernel;

    /// <summary>
    /// Invokes a plugin function
    /// </summary>
    public async Task<FunctionResult> InvokeAsync(
        string pluginName,
        string functionName,
        KernelArguments? arguments = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var function = _kernel.Plugins[pluginName][functionName];
            return await _kernel.InvokeAsync(function, arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking plugin function: {PluginName}.{FunctionName}",
                pluginName, functionName);
            throw;
        }
    }

    /// <summary>
    /// Gets a list of all available plugins
    /// </summary>
    public IEnumerable<KernelPlugin> GetPlugins() => _kernel.Plugins;
}
