using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class FollowUpGeneratorTests
{
    private readonly ILLMService _llmService;
    private readonly ILogger<FollowUpGenerator> _logger;

    public FollowUpGeneratorTests()
    {
        _llmService = Substitute.For<ILLMService>();
        _logger = Substitute.For<ILogger<FollowUpGenerator>>();
    }

    [Fact]
    public async Task GenerateAsync_WithValidContext_ShouldReturnFollowUpQuestions()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "What is the capital of France?",
            Response = "The capital of France is Paris.",
            TrackedEntities = new List<string> { "France", "Paris" }
        };

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("""
                1. What is the population of Paris?
                2. What are the main attractions in Paris?
                3. How does Paris compare to other European capitals?
                """));

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FollowUpQuestions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ShouldGenerateRuleBasedQuestions_WhenLLMFails()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "Tell me about Apple Inc",
            Response = "Apple Inc is a technology company.",
            TrackedEntities = new List<string> { "Apple Inc" }
        };

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(Error.Internal("LLM.Error", "Service unavailable")));

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FollowUpQuestions.Should().NotBeEmpty();
        result.Value.GenerationMethod.Should().Be("rule-based");
    }

    [Fact]
    public async Task GenerateAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "",
            Response = "Some response"
        };

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_ShouldRespectMaxQuestions()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "What is AI?",
            Response = "AI is artificial intelligence.",
            MaxQuestions = 2
        };

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("""
                1. How does AI work?
                2. What are the applications of AI?
                3. What is machine learning?
                4. Who invented AI?
                """));

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FollowUpQuestions.Should().HaveCountLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeEntityBasedQuestions()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "What does Microsoft do?",
            Response = "Microsoft develops software and cloud services.",
            TrackedEntities = new List<string> { "Microsoft" }
        };

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(Error.Internal("LLM.Error", "Unavailable")));

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FollowUpQuestions.Should().Contain(q => q.Contains("Microsoft"));
    }

    [Fact]
    public async Task GenerateAsync_ShouldCategorizeQuestions()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "What is Tesla's stock price?",
            Response = "Tesla's current stock price is $250.",
            TrackedEntities = new List<string> { "Tesla" }
        };

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("""
                1. What is Tesla's market cap?
                2. How has Tesla's stock performed this year?
                3. What are analysts saying about Tesla?
                """));

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategorizedQuestions.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WithConversationContext_ShouldConsiderHistory()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "What is their revenue?",
            Response = "Apple's revenue is $394 billion.",
            TrackedEntities = new List<string> { "Apple" },
            ConversationHistory = new List<string>
            {
                "User: Tell me about Apple",
                "Assistant: Apple is a technology company..."
            }
        };

        _llmService.GenerateResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("1. How does Apple's revenue compare to competitors?"));

        // Act
        var result = await generator.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _llmService.Received(1).GenerateResponseAsync(
            Arg.Is<string>(s => s.Contains("Apple")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRuleBasedAsync_ShouldGenerateQuestionsWithoutLLM()
    {
        // Arrange
        var generator = new FollowUpGenerator(_llmService, _logger);
        var request = new FollowUpRequest
        {
            OriginalQuery = "What is the weather in New York?",
            Response = "It's sunny and 75°F in New York.",
            TrackedEntities = new List<string> { "New York" }
        };

        // Act
        var result = await generator.GenerateRuleBasedAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FollowUpQuestions.Should().NotBeEmpty();
        result.Value.GenerationMethod.Should().Be("rule-based");
    }
}
