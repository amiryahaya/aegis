using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.LLM;
using FluentAssertions;

namespace Aegis.UnitTests.Services.LLM;

public class LLMServiceTests
{
    [Fact]
    public async Task GenerateResponseAsync_WithValidPrompt_ShouldReturnResponse()
    {
        // Arrange
        var llmService = new MockLLMService();
        var prompt = "What is machine learning?";

        // Act
        var result = await llmService.GenerateResponseAsync(prompt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateResponseAsync_WithEmptyPrompt_ShouldReturnFailure()
    {
        // Arrange
        var llmService = new MockLLMService();

        // Act
        var result = await llmService.GenerateResponseAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("EmptyPrompt");
    }

    [Fact]
    public async Task GenerateRAGResponseAsync_WithContext_ShouldIncludeContext()
    {
        // Arrange
        var llmService = new MockLLMService();
        var query = "What is quantum computing?";
        var contexts = new List<string>
        {
            "Quantum computing uses quantum bits or qubits.",
            "Quantum computers can solve certain problems faster than classical computers.",
            "Quantum algorithms include Shor's algorithm and Grover's algorithm."
        };

        // Act
        var result = await llmService.GenerateRAGResponseAsync(query, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Should().NotBeNullOrWhiteSpace();
        result.Value.Citations.Should().NotBeEmpty();
        result.Value.Citations.Should().HaveCount(contexts.Count);
    }

    [Fact]
    public async Task GenerateRAGResponseAsync_WithEmptyContext_ShouldReturnFailure()
    {
        // Arrange
        var llmService = new MockLLMService();
        var query = "What is quantum computing?";
        var contexts = new List<string>();

        // Act
        var result = await llmService.GenerateRAGResponseAsync(query, contexts);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("EmptyContext");
    }

    [Fact]
    public async Task GenerateRAGResponseAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var llmService = new MockLLMService();
        var contexts = new List<string> { "Some context" };

        // Act
        var result = await llmService.GenerateRAGResponseAsync("", contexts);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("EmptyQuery");
    }

    [Fact]
    public async Task GenerateRAGResponseAsync_ShouldIncludeCitationIndices()
    {
        // Arrange
        var llmService = new MockLLMService();
        var query = "Explain machine learning";
        var contexts = new List<string>
        {
            "Machine learning is a subset of AI.",
            "It involves training models on data.",
            "Common algorithms include neural networks."
        };

        // Act
        var result = await llmService.GenerateRAGResponseAsync(query, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Citations.Should().AllSatisfy(citation =>
        {
            citation.Index.Should().BeGreaterOrEqualTo(0);
            citation.Index.Should().BeLessThan(contexts.Count);
            citation.Text.Should().Be(contexts[citation.Index]);
        });
    }

    [Fact]
    public async Task GenerateStreamingResponseAsync_ShouldStreamTokens()
    {
        // Arrange
        var llmService = new MockLLMService();
        var prompt = "Tell me about artificial intelligence";
        var tokens = new List<string>();

        // Act
        var result = llmService.GenerateStreamingResponseAsync(prompt);

        await foreach (var token in result)
        {
            if (token.IsSuccess)
            {
                tokens.Add(token.Value);
            }
        }

        // Assert
        tokens.Should().NotBeEmpty();
        string.Join("", tokens).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateStreamingResponseAsync_WithEmptyPrompt_ShouldReturnError()
    {
        // Arrange
        var llmService = new MockLLMService();
        var hasError = false;

        // Act
        var result = llmService.GenerateStreamingResponseAsync("");

        await foreach (var token in result)
        {
            if (token.IsFailure)
            {
                hasError = true;
                token.Error!.Code.Should().Contain("EmptyPrompt");
                break;
            }
        }

        // Assert
        hasError.Should().BeTrue();
    }
}
