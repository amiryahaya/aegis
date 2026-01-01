using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class RefinementLoopTests
{
    private readonly IEvaluatorAgent _evaluatorAgent;
    private readonly ILLMService _llmService;
    private readonly ILogger<RefinementLoop> _logger;

    public RefinementLoopTests()
    {
        _evaluatorAgent = Substitute.For<IEvaluatorAgent>();
        _llmService = Substitute.For<ILLMService>();
        _logger = Substitute.For<ILogger<RefinementLoop>>();
    }

    [Fact]
    public async Task RefineAsync_WhenResponseDoesNotNeedRefinement_ShouldReturnOriginalResponse()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "What is the capital of France?",
            OriginalResponse = "The capital of France is Paris.",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EvaluationResult>.Success(new EvaluationResult
            {
                CompletenessScore = 0.9,
                FaithfulnessScore = 0.95,
                OverallScore = 0.9,
                NeedsRefinement = false
            }));

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FinalResponse.Should().Be(request.OriginalResponse);
        result.Value.WasRefined.Should().BeFalse();
        result.Value.RefinementIterations.Should().Be(0);
    }

    [Fact]
    public async Task RefineAsync_WhenResponseNeedsRefinement_ShouldRefineResponse()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "What is the capital of France?",
            OriginalResponse = "Paris is a city.",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        // First evaluation - needs refinement
        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<EvaluationResult>.Success(new EvaluationResult
                {
                    CompletenessScore = 0.4,
                    FaithfulnessScore = 0.5,
                    OverallScore = 0.45,
                    NeedsRefinement = true,
                    Suggestions = new List<string> { "Include that Paris is the capital" }
                }),
                Result<EvaluationResult>.Success(new EvaluationResult
                {
                    CompletenessScore = 0.9,
                    FaithfulnessScore = 0.95,
                    OverallScore = 0.9,
                    NeedsRefinement = false
                }));

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("The capital of France is Paris, which is a major European city."));

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WasRefined.Should().BeTrue();
        result.Value.RefinementIterations.Should().BeGreaterThan(0);
        result.Value.FinalResponse.Should().NotBe(request.OriginalResponse);
    }

    [Fact]
    public async Task RefineAsync_ShouldRespectMaxIterations()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "Complex question",
            OriginalResponse = "Incomplete answer",
            SourceContexts = new List<string> { "Some context" },
            MaxIterations = 3
        };

        // Always needs refinement (should stop at max iterations)
        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EvaluationResult>.Success(new EvaluationResult
            {
                CompletenessScore = 0.4,
                FaithfulnessScore = 0.5,
                OverallScore = 0.45,
                NeedsRefinement = true
            }));

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Still incomplete"));

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RefinementIterations.Should().BeLessThanOrEqualTo(3);
        result.Value.ReachedMaxIterations.Should().BeTrue();
    }

    [Fact]
    public async Task RefineAsync_ShouldTrackRefinementHistory()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "What is AI?",
            OriginalResponse = "AI exists",
            SourceContexts = new List<string> { "AI is artificial intelligence" }
        };

        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<EvaluationResult>.Success(new EvaluationResult { NeedsRefinement = true, OverallScore = 0.3 }),
                Result<EvaluationResult>.Success(new EvaluationResult { NeedsRefinement = false, OverallScore = 0.85 }));

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("AI, or artificial intelligence, is the simulation of human intelligence."));

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RefinementHistory.Should().NotBeEmpty();
        result.Value.RefinementHistory.Should().AllSatisfy(h =>
        {
            h.Response.Should().NotBeEmpty();
            h.EvaluationScore.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task RefineAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "",
            OriginalResponse = "Some response",
            SourceContexts = new List<string>()
        };

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task RefineAsync_ShouldIncludeImprovementInPrompt()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "What is the capital?",
            OriginalResponse = "Unknown",
            SourceContexts = new List<string> { "Paris is the capital" }
        };

        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<EvaluationResult>.Success(new EvaluationResult
                {
                    NeedsRefinement = true,
                    OverallScore = 0.2,
                    Suggestions = new List<string> { "Use the source information", "Be more specific" }
                }),
                Result<EvaluationResult>.Success(new EvaluationResult { NeedsRefinement = false, OverallScore = 0.9 }));

        string capturedPrompt = "";
        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedPrompt = callInfo.ArgAt<string>(0);
                return Result<string>.Success("Paris is the capital");
            });

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedPrompt.Should().Contain("Use the source information");
    }

    [Fact]
    public async Task RefineAsync_WhenLLMFails_ShouldReturnOriginalResponse()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "What is AI?",
            OriginalResponse = "AI is technology",
            SourceContexts = new List<string> { "AI definition" }
        };

        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EvaluationResult>.Success(new EvaluationResult { NeedsRefinement = true }));

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(Error.Internal("LLM.Error", "Service unavailable")));

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FinalResponse.Should().Be(request.OriginalResponse);
        result.Value.HadErrors.Should().BeTrue();
    }

    [Fact]
    public async Task RefineAsync_ShouldCalculateScoreImprovement()
    {
        // Arrange
        var refinementLoop = new RefinementLoop(_evaluatorAgent, _llmService, _logger);
        var request = new RefinementRequest
        {
            Query = "Explain quantum computing",
            OriginalResponse = "It's complex",
            SourceContexts = new List<string> { "Quantum computing uses qubits" }
        };

        _evaluatorAgent.EvaluateAsync(Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<EvaluationResult>.Success(new EvaluationResult { NeedsRefinement = true, OverallScore = 0.2 }),
                Result<EvaluationResult>.Success(new EvaluationResult { NeedsRefinement = false, OverallScore = 0.85 }));

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Quantum computing uses qubits to perform complex calculations."));

        // Act
        var result = await refinementLoop.RefineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InitialScore.Should().BeApproximately(0.2, 0.01);
        result.Value.FinalScore.Should().BeApproximately(0.85, 0.01);
        result.Value.ScoreImprovement.Should().BeApproximately(0.65, 0.01);
    }
}
