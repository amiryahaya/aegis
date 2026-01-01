using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Generates suggested follow-up questions based on conversation context
/// </summary>
public class FollowUpGenerator : IFollowUpGenerator
{
    private readonly ILLMService _llmService;
    private readonly ILogger<FollowUpGenerator> _logger;

    private static readonly Dictionary<string, string[]> QuestionTemplates = new()
    {
        ["entity"] = new[]
        {
            "What else can you tell me about {entity}?",
            "How is {entity} related to other entities?",
            "What is the history of {entity}?",
            "What are the key facts about {entity}?"
        },
        ["comparison"] = new[]
        {
            "How does this compare to similar cases?",
            "What are the alternatives?",
            "What are the pros and cons?"
        },
        ["detail"] = new[]
        {
            "Can you provide more details?",
            "What are the specific examples?",
            "Can you explain this further?"
        },
        ["impact"] = new[]
        {
            "What are the implications of this?",
            "How does this affect related areas?",
            "What are the potential consequences?"
        },
        ["temporal"] = new[]
        {
            "What happened before this?",
            "What is expected to happen next?",
            "How has this changed over time?"
        }
    };

    public FollowUpGenerator(
        ILLMService llmService,
        ILogger<FollowUpGenerator> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Result<FollowUpResult>> GenerateAsync(
        FollowUpRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reasoningSteps = new List<string>();

            // Validate input
            if (string.IsNullOrWhiteSpace(request.OriginalQuery))
            {
                return Result<FollowUpResult>.Failure(
                    Error.Validation("FollowUp.QueryRequired", "Original query is required"));
            }

            reasoningSteps.Add("Analyzing conversation context for follow-up generation");

            // Try LLM-based generation first
            var llmResult = await GenerateLLMBasedAsync(request, cancellationToken);

            if (llmResult.IsSuccess && llmResult.Value.FollowUpQuestions.Count > 0)
            {
                reasoningSteps.Add($"Generated {llmResult.Value.FollowUpQuestions.Count} questions using LLM");
                return Result<FollowUpResult>.Success(llmResult.Value with
                {
                    ReasoningSteps = reasoningSteps
                });
            }

            // Fallback to rule-based generation
            reasoningSteps.Add("LLM generation failed or returned no results, falling back to rule-based");
            var ruleBasedResult = await GenerateRuleBasedAsync(request, cancellationToken);

            if (ruleBasedResult.IsSuccess)
            {
                reasoningSteps.AddRange(ruleBasedResult.Value.ReasoningSteps);
                return Result<FollowUpResult>.Success(ruleBasedResult.Value with
                {
                    ReasoningSteps = reasoningSteps
                });
            }

            return ruleBasedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating follow-up questions");
            return Result<FollowUpResult>.Failure(
                Error.Internal("FollowUp.Error", ex.Message));
        }
    }

    public Task<Result<FollowUpResult>> GenerateRuleBasedAsync(
        FollowUpRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reasoningSteps = new List<string>();
            var questions = new List<string>();
            var categorized = new Dictionary<string, List<string>>();

            reasoningSteps.Add("Generating follow-up questions using rule-based approach");

            // Generate entity-based questions
            if (request.TrackedEntities.Count > 0)
            {
                var entityQuestions = new List<string>();
                foreach (var entity in request.TrackedEntities.Take(2))
                {
                    var template = QuestionTemplates["entity"][0];
                    entityQuestions.Add(template.Replace("{entity}", entity));
                }
                questions.AddRange(entityQuestions);
                categorized["entity"] = entityQuestions;
                reasoningSteps.Add($"Generated {entityQuestions.Count} entity-based questions");
            }

            // Analyze query type and add appropriate questions
            var queryType = AnalyzeQueryType(request.OriginalQuery);
            reasoningSteps.Add($"Detected query type: {queryType}");

            var typeQuestions = GenerateTypeBasedQuestions(queryType, request);
            questions.AddRange(typeQuestions);
            if (typeQuestions.Count > 0)
            {
                categorized[queryType] = typeQuestions;
            }

            // Add general follow-up if we don't have enough
            if (questions.Count < request.MaxQuestions)
            {
                var generalQuestions = GenerateGeneralQuestions(request);
                questions.AddRange(generalQuestions);
                if (generalQuestions.Count > 0)
                {
                    categorized["general"] = generalQuestions;
                }
            }

            // Limit to max questions
            questions = questions.Take(request.MaxQuestions).ToList();

            var result = new FollowUpResult
            {
                FollowUpQuestions = questions,
                CategorizedQuestions = categorized,
                GenerationMethod = "rule-based",
                Confidence = 0.7,
                ReasoningSteps = reasoningSteps
            };

            return Task.FromResult(Result<FollowUpResult>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rule-based follow-up generation");
            return Task.FromResult(Result<FollowUpResult>.Failure(
                Error.Internal("FollowUp.RuleBasedError", ex.Message)));
        }
    }

    private async Task<Result<FollowUpResult>> GenerateLLMBasedAsync(
        FollowUpRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = BuildFollowUpPrompt(request);
        var llmResult = await _llmService.GenerateResponseAsync(prompt, cancellationToken);

        if (llmResult.IsFailure)
        {
            return Result<FollowUpResult>.Failure(llmResult.Error!);
        }

        var questions = ParseLLMResponse(llmResult.Value);

        // Limit to max questions
        questions = questions.Take(request.MaxQuestions).ToList();

        var categorized = CategorizeQuestions(questions);

        return Result<FollowUpResult>.Success(new FollowUpResult
        {
            FollowUpQuestions = questions,
            CategorizedQuestions = categorized,
            GenerationMethod = "llm",
            Confidence = 0.85
        });
    }

    private string BuildFollowUpPrompt(FollowUpRequest request)
    {
        var entitiesText = request.TrackedEntities.Count > 0
            ? $"Entities mentioned: {string.Join(", ", request.TrackedEntities)}"
            : "";

        var historyText = request.ConversationHistory.Count > 0
            ? $"Conversation history:\n{string.Join("\n", request.ConversationHistory.TakeLast(3))}"
            : "";

        return $"""
            Based on the following conversation, generate {request.MaxQuestions} relevant follow-up questions that the user might want to ask next.

            Original Question: {request.OriginalQuery}

            Response: {request.Response}

            {entitiesText}

            {historyText}

            Generate follow-up questions that:
            1. Build on the information provided
            2. Explore related topics
            3. Ask for more detail or clarification
            4. Are relevant to the entities and topics discussed

            Format: Return each question on a new line, numbered 1-{request.MaxQuestions}.

            Follow-up questions:
            """;
    }

    private List<string> ParseLLMResponse(string response)
    {
        var questions = new List<string>();

        // Split by newlines and extract questions
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var cleaned = line.Trim();

            // Remove numbering like "1.", "1)", "- ", etc.
            cleaned = Regex.Replace(cleaned, @"^[\d]+[\.\)]\s*", "");
            cleaned = Regex.Replace(cleaned, @"^[-•]\s*", "");
            cleaned = cleaned.Trim();

            // Only add if it looks like a question
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 10)
            {
                // Ensure it ends with a question mark
                if (!cleaned.EndsWith("?"))
                {
                    cleaned += "?";
                }
                questions.Add(cleaned);
            }
        }

        return questions;
    }

    private string AnalyzeQueryType(string query)
    {
        var queryLower = query.ToLowerInvariant();

        if (queryLower.Contains("compare") || queryLower.Contains("difference") ||
            queryLower.Contains("versus") || queryLower.Contains(" vs "))
        {
            return "comparison";
        }

        if (queryLower.Contains("when") || queryLower.Contains("timeline") ||
            queryLower.Contains("history") || queryLower.Contains("before") ||
            queryLower.Contains("after"))
        {
            return "temporal";
        }

        if (queryLower.Contains("impact") || queryLower.Contains("effect") ||
            queryLower.Contains("consequence") || queryLower.Contains("result"))
        {
            return "impact";
        }

        if (queryLower.Contains("explain") || queryLower.Contains("detail") ||
            queryLower.Contains("how does") || queryLower.Contains("why"))
        {
            return "detail";
        }

        return "general";
    }

    private List<string> GenerateTypeBasedQuestions(string queryType, FollowUpRequest request)
    {
        var questions = new List<string>();

        if (QuestionTemplates.TryGetValue(queryType, out var templates))
        {
            questions.Add(templates[0]);
            if (templates.Length > 1 && request.MaxQuestions > 1)
            {
                questions.Add(templates[1]);
            }
        }

        return questions;
    }

    private List<string> GenerateGeneralQuestions(FollowUpRequest request)
    {
        var questions = new List<string>();

        if (request.TrackedEntities.Count > 0)
        {
            var entity = request.TrackedEntities.First();
            questions.Add($"What are the latest developments regarding {entity}?");
        }

        questions.Add("Can you provide sources for this information?");
        questions.Add("Are there any related topics I should explore?");

        return questions;
    }

    private Dictionary<string, List<string>> CategorizeQuestions(List<string> questions)
    {
        var categorized = new Dictionary<string, List<string>>
        {
            ["exploratory"] = new List<string>(),
            ["clarification"] = new List<string>(),
            ["related"] = new List<string>()
        };

        foreach (var question in questions)
        {
            var questionLower = question.ToLowerInvariant();

            if (questionLower.Contains("more") || questionLower.Contains("detail") ||
                questionLower.Contains("explain") || questionLower.Contains("mean"))
            {
                categorized["clarification"].Add(question);
            }
            else if (questionLower.Contains("related") || questionLower.Contains("similar") ||
                     questionLower.Contains("compare") || questionLower.Contains("other"))
            {
                categorized["related"].Add(question);
            }
            else
            {
                categorized["exploratory"].Add(question);
            }
        }

        // Remove empty categories
        return categorized.Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
