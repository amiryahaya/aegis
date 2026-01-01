using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class EvaluatorAgentTests
{
    private readonly ILLMService _llmService;
    private readonly ILogger<EvaluatorAgent> _logger;

    public EvaluatorAgentTests()
    {
        _llmService = Substitute.For<ILLMService>();
        _logger = Substitute.For<ILogger<EvaluatorAgent>>();
    }

    [Fact]
    public void AgentType_ShouldReturnEvaluator()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);

        // Act
        var agentType = agent.AgentType;

        // Assert
        agentType.Should().Be("Evaluator");
    }

    [Fact]
    public async Task EvaluateAsync_WithValidResponse_ShouldReturnEvaluationResult()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "The capital of France is Paris.",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CompletenessScore.Should().BeGreaterThanOrEqualTo(0);
        result.Value.CompletenessScore.Should().BeLessThanOrEqualTo(1);
        result.Value.FaithfulnessScore.Should().BeGreaterThanOrEqualTo(0);
        result.Value.FaithfulnessScore.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "",
            Response = "Some response",
            SourceContexts = new List<string>()
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Contain("Query");
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyResponse_ShouldReturnLowScores()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompletenessScore.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_WithNoSourceContexts_ShouldReturnLowFaithfulness()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "The capital of France is Paris.",
            SourceContexts = new List<string>()
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FaithfulnessScore.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldIncludeImprovementSuggestions()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France and what is its population?",
            Response = "The capital of France is Paris.",
            SourceContexts = new List<string>
            {
                "Paris is the capital city of France.",
                "Paris has a population of approximately 2.1 million."
            }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Suggestions.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithHighQualityResponse_ShouldReturnHighScores()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "The capital of France is Paris. Paris is a major European city and the political center of France.",
            SourceContexts = new List<string>
            {
                "Paris is the capital city of France.",
                "Paris is a major European city and serves as the political center of France."
            }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompletenessScore.Should().BeGreaterThan(0.5);
        result.Value.FaithfulnessScore.Should().BeGreaterThan(0.5);
        result.Value.OverallScore.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldCalculateOverallScore()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "The capital of France is Paris.",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OverallScore.Should().BeGreaterThanOrEqualTo(0);
        result.Value.OverallScore.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldIncludeReasoningSteps()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "The capital of France is Paris.",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReasoningSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ShouldReturnAgentResponse()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new AgentRequest
        {
            RequestId = Guid.NewGuid(),
            Task = "Evaluate response quality",
            Context = new Dictionary<string, object>
            {
                { "query", "What is AI?" },
                { "response", "AI is artificial intelligence." },
                { "sources", new List<string> { "Artificial Intelligence (AI) is the simulation of human intelligence." } }
            }
        };

        // Act
        var result = await agent.ExecuteAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Result.Should().NotBeEmpty();
        result.Value.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldDetectHallucination()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "What is the capital of France?",
            Response = "The capital of France is London, which is also the largest city in the country.",
            SourceContexts = new List<string> { "Paris is the capital city of France." }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FaithfulnessScore.Should().BeLessThan(0.5);
        result.Value.HasPotentialHallucination.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnNeedsRefinementFlag()
    {
        // Arrange
        var agent = new EvaluatorAgent(_llmService, _logger);
        var request = new EvaluationRequest
        {
            Query = "Explain the complete history of artificial intelligence",
            Response = "AI was invented.",
            SourceContexts = new List<string>
            {
                "Artificial intelligence was first conceptualized in the 1950s by pioneers like Alan Turing.",
                "The field has evolved through multiple phases including symbolic AI, machine learning, and deep learning."
            }
        };

        // Act
        var result = await agent.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NeedsRefinement.Should().BeTrue();
    }
}
