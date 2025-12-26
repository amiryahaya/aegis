using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Agent responsible for analyzing retrieved information
/// </summary>
public class AnalyzerAgent : IAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<AnalyzerAgent> _logger;

    public string AgentType => "Analyzer";

    public AnalyzerAgent(
        ILLMService llmService,
        ILogger<AnalyzerAgent> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Result<AgentResponse>> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzer agent executing: {Task}", request.Task);

            // Collect data from previous steps
            var contexts = new List<string>();
            foreach (var kvp in request.Context)
            {
                if (kvp.Key.StartsWith("dependency_"))
                {
                    contexts.Add(kvp.Value?.ToString() ?? "");
                }
            }

            // Build analysis prompt
            var analysisPrompt = BuildAnalysisPrompt(request.Task, contexts);

            // Generate analysis using LLM
            var llmResult = await _llmService.GenerateResponseAsync(analysisPrompt, cancellationToken);

            if (llmResult.IsFailure)
            {
                return Result<AgentResponse>.Failure(llmResult.Error!);
            }

            var response = new AgentResponse
            {
                Result = llmResult.Value,
                Confidence = 0.85,
                Metadata = new Dictionary<string, object>
                {
                    { "contextCount", contexts.Count },
                    { "analysisType", "llm-based" }
                },
                ReasoningSteps = new List<string>
                {
                    $"Collected {contexts.Count} context items from previous tasks",
                    "Built analysis prompt",
                    "Generated analysis using LLM"
                }
            };

            return Result<AgentResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Analyzer agent");
            return Result<AgentResponse>.Failure(
                Error.Internal("Analyzer.Error", ex.Message));
        }
    }

    private string BuildAnalysisPrompt(string task, List<string> contexts)
    {
        var contextText = string.Join("\n\n", contexts.Select((c, i) => $"[Context {i + 1}]\n{c}"));

        return $"""
            You are an intelligence analyst. Analyze the following information and provide insights.

            Task: {task}

            Information Retrieved:
            {contextText}

            Provide a detailed analysis including:
            1. Key findings
            2. Patterns or connections
            3. Potential risks or concerns
            4. Recommendations for further investigation

            Analysis:
            """;
    }
}
