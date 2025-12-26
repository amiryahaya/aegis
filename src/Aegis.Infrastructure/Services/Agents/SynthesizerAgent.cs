using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Agent responsible for synthesizing final responses from all task results
/// </summary>
public class SynthesizerAgent : IAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<SynthesizerAgent> _logger;

    public string AgentType => "Synthesizer";

    public SynthesizerAgent(
        ILLMService llmService,
        ILogger<SynthesizerAgent> logger)
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
            _logger.LogInformation("Synthesizer agent executing: {Task}", request.Task);

            // Collect all previous results
            var allResults = new List<string>();
            foreach (var kvp in request.Context)
            {
                if (kvp.Key.StartsWith("dependency_"))
                {
                    allResults.Add(kvp.Value?.ToString() ?? "");
                }
            }

            // Build synthesis prompt
            var synthesisPrompt = BuildSynthesisPrompt(request.Task, allResults);

            // Generate final response using LLM
            var llmResult = await _llmService.GenerateResponseAsync(synthesisPrompt, cancellationToken);

            if (llmResult.IsFailure)
            {
                return Result<AgentResponse>.Failure(llmResult.Error!);
            }

            var response = new AgentResponse
            {
                Result = llmResult.Value,
                Confidence = 0.9,
                Metadata = new Dictionary<string, object>
                {
                    { "sourceCount", allResults.Count },
                    { "synthesisType", "llm-based" }
                },
                ReasoningSteps = new List<string>
                {
                    $"Collected results from {allResults.Count} previous tasks",
                    "Built comprehensive synthesis prompt",
                    "Generated final coherent response"
                }
            };

            return Result<AgentResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Synthesizer agent");
            return Result<AgentResponse>.Failure(
                Error.Internal("Synthesizer.Error", ex.Message));
        }
    }

    private string BuildSynthesisPrompt(string originalQuery, List<string> allResults)
    {
        var resultsText = string.Join("\n\n", allResults.Select((r, i) => $"[Result {i + 1}]\n{r}"));

        return $"""
            You are synthesizing information from multiple sources to answer a user's query.

            Original Query: {originalQuery}

            Information from Various Agents:
            {resultsText}

            Instructions:
            1. Synthesize all the information above into a coherent, comprehensive answer
            2. Address the original query directly
            3. Include relevant details, findings, and insights
            4. Highlight any important connections or patterns
            5. Provide actionable recommendations if applicable
            6. Use clear, professional language

            Final Response:
            """;
    }
}
