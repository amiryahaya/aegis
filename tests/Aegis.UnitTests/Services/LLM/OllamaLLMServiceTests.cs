using System.Net;
using System.Text.Json;
using Aegis.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Aegis.UnitTests.Services.LLM;

public class OllamaLLMServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLMService> _logger;

    public OllamaLLMServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _logger = Substitute.For<ILogger<OllamaLLMService>>();
    }

    [Fact]
    public async Task GenerateResponseAsync_WithValidPrompt_ShouldReturnResponse()
    {
        // Arrange
        var responsePayload = new
        {
            model = "qwen2.5:latest",
            response = "This is a test response from Ollama",
            done = true
        };

        _mockHttp.When("http://localhost:11434/api/generate")
            .Respond("application/json", JsonSerializer.Serialize(responsePayload));

        var service = new OllamaLLMService(_httpClient, _logger);

        // Act
        var result = await service.GenerateResponseAsync("Test prompt");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("This is a test response from Ollama");
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenOllamaUnavailable_ShouldReturnFailure()
    {
        // Arrange
        _mockHttp.When("http://localhost:11434/api/generate")
            .Respond(HttpStatusCode.ServiceUnavailable);

        var service = new OllamaLLMService(_httpClient, _logger);

        // Act
        var result = await service.GenerateResponseAsync("Test prompt");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Contain("Ollama");
    }

    [Fact]
    public async Task GenerateRAGResponseAsync_WithContexts_ShouldGenerateResponse()
    {
        // Arrange
        var responsePayload = new
        {
            model = "qwen2.5:latest",
            response = "Based on the provided context [1], the answer is...",
            done = true
        };

        _mockHttp.When("http://localhost:11434/api/generate")
            .Respond("application/json", JsonSerializer.Serialize(responsePayload));

        var service = new OllamaLLMService(_httpClient, _logger);
        var contexts = new List<string>
        {
            "Context passage 1 about the topic",
            "Context passage 2 with more details"
        };

        // Act
        var result = await service.GenerateRAGResponseAsync("What is this about?", contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Should().Contain("answer");
        result.Value.Citations.Should().HaveCount(1);
        result.Value.Citations[0].Index.Should().Be(0);
    }
}
