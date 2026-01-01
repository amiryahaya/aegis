using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Caching;

public class ResponseCacheTests
{
    private readonly ILogger<InMemoryResponseCache> _logger;
    private readonly InMemoryResponseCache _sut;

    public ResponseCacheTests()
    {
        _logger = Substitute.For<ILogger<InMemoryResponseCache>>();
        _sut = new InMemoryResponseCache(_logger);
    }

    #region Cache Miss

    [Fact]
    public async Task GetAsync_WithNoCache_ReturnsMiss()
    {
        // Arrange
        var request = new ResponseCacheRequest
        {
            Prompt = "What is the capital of France?",
            Model = "gpt-4"
        };

        // Act
        var result = await _sut.GetAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeFalse();
        result.Value.Response.Should().BeNull();
    }

    #endregion

    #region Cache Hit

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsHit()
    {
        // Arrange
        var prompt = "What is 2+2?";
        var response = "The answer is 4.";
        var model = "gpt-4";

        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = prompt,
            Response = response,
            Model = model,
            Temperature = 0.7
        });

        // Act
        var result = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = prompt,
            Model = model,
            Temperature = 0.7
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeTrue();
        result.Value.Response.Should().Be(response);
        result.Value.Model.Should().Be(model);
        result.Value.MatchScore.Should().Be(1.0);
        result.Value.IsFuzzyMatch.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_DifferentTemperature_ReturnsMiss()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Test prompt",
            Response = "Test response",
            Model = "gpt-4",
            Temperature = 0.0
        });

        // Act - Different temperature
        var result = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "Test prompt",
            Model = "gpt-4",
            Temperature = 1.0
        });

        // Assert
        result.Value.IsHit.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_DifferentModel_ReturnsMiss()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Test prompt",
            Response = "GPT-4 response",
            Model = "gpt-4",
            Temperature = 0.7
        });

        // Act
        var result = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "Test prompt",
            Model = "gpt-3.5-turbo",
            Temperature = 0.7
        });

        // Assert
        result.Value.IsHit.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_WithSystemPrompt_MatchesCorrectly()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Hello",
            SystemPrompt = "You are a helpful assistant.",
            Response = "Hi there!",
            Model = "gpt-4",
            Temperature = 0.7
        });

        // Act - Same system prompt
        var hitResult = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "Hello",
            SystemPrompt = "You are a helpful assistant.",
            Model = "gpt-4",
            Temperature = 0.7
        });

        // Act - Different system prompt
        var missResult = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "Hello",
            SystemPrompt = "You are a pirate.",
            Model = "gpt-4",
            Temperature = 0.7
        });

        // Assert
        hitResult.Value.IsHit.Should().BeTrue();
        missResult.Value.IsHit.Should().BeFalse();
    }

    #endregion

    #region Workspace Scoping

    [Fact]
    public async Task GetAsync_DifferentWorkspace_ReturnsMiss()
    {
        // Arrange
        var workspace1 = Guid.NewGuid();
        var workspace2 = Guid.NewGuid();

        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Workspace query",
            Response = "Workspace response",
            Model = "gpt-4",
            WorkspaceId = workspace1
        });

        // Act
        var result = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "Workspace query",
            Model = "gpt-4",
            WorkspaceId = workspace2
        });

        // Assert
        result.Value.IsHit.Should().BeFalse();
    }

    #endregion

    #region Cost and Latency Tracking

    [Fact]
    public async Task GetAsync_ReturnsCostSavings()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Expensive query",
            Response = "Expensive response",
            Model = "gpt-4",
            Temperature = 0.7,
            InputTokens = 100,
            OutputTokens = 200,
            LatencyMs = 500,
            EstimatedCost = 0.05m
        });

        // Act
        var result = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "Expensive query",
            Model = "gpt-4",
            Temperature = 0.7
        });

        // Assert
        result.Value.IsHit.Should().BeTrue();
        result.Value.OriginalLatencyMs.Should().Be(500);
        result.Value.CostSavings.Should().Be(0.05m);
    }

    [Fact]
    public async Task SetAsync_StoresTokenCounts()
    {
        // Arrange
        var entry = new ResponseCacheEntry
        {
            Prompt = "Token test",
            Response = "Token response",
            Model = "gpt-4",
            Temperature = 0.7,
            InputTokens = 50,
            OutputTokens = 100
        };

        // Act
        await _sut.SetAsync(entry);
        var result = await _sut.GetAsync(new ResponseCacheRequest { Prompt = "Token test", Model = "gpt-4", Temperature = 0.7 });

        // Assert
        result.Value.TokenCount.Should().Be(100);
    }

    #endregion

    #region Invalidation

    [Fact]
    public async Task InvalidateAsync_ByModel_InvalidatesModelEntries()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry { Prompt = "P1", Response = "R1", Model = "gpt-4", Temperature = 0 });
        await _sut.SetAsync(new ResponseCacheEntry { Prompt = "P2", Response = "R2", Model = "gpt-3.5", Temperature = 0 });

        // Act
        var result = await _sut.InvalidateAsync(new ResponseInvalidationPattern { Model = "gpt-4" });

        // Assert
        result.Value.Should().Be(1);

        var get1 = await _sut.GetAsync(new ResponseCacheRequest { Prompt = "P1", Model = "gpt-4", Temperature = 0 });
        var get2 = await _sut.GetAsync(new ResponseCacheRequest { Prompt = "P2", Model = "gpt-3.5", Temperature = 0 });

        get1.Value.IsHit.Should().BeFalse();
        get2.Value.IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidateAsync_ByWorkspace_InvalidatesWorkspaceEntries()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "WS query",
            Response = "WS response",
            Model = "gpt-4",
            WorkspaceId = workspaceId
        });

        // Act
        var result = await _sut.InvalidateAsync(new ResponseInvalidationPattern { WorkspaceId = workspaceId });

        // Assert
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task InvalidateAsync_ByTags_InvalidatesTaggedEntries()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Tagged",
            Response = "Response",
            Model = "gpt-4",
            Tags = new List<string> { "finance", "report" }
        });

        // Act
        var result = await _sut.InvalidateAsync(new ResponseInvalidationPattern
        {
            Tags = new List<string> { "finance" }
        });

        // Assert
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task InvalidateAsync_All_ClearsCache()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _sut.SetAsync(new ResponseCacheEntry
            {
                Prompt = $"Prompt {i}",
                Response = $"Response {i}",
                Model = "gpt-4"
            });
        }

        // Act
        var result = await _sut.InvalidateAsync(new ResponseInvalidationPattern { InvalidateAll = true });

        // Assert
        result.Value.Should().Be(10);
    }

    #endregion

    #region Statistics

    [Fact]
    public async Task GetStatisticsAsync_TracksMetrics()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "Stats query",
            Response = "Stats response",
            Model = "gpt-4",
            Temperature = 0,
            InputTokens = 50,
            OutputTokens = 100,
            LatencyMs = 200,
            EstimatedCost = 0.02m
        });

        // Generate hits and misses
        await _sut.GetAsync(new ResponseCacheRequest { Prompt = "Stats query", Model = "gpt-4", Temperature = 0 }); // Hit
        await _sut.GetAsync(new ResponseCacheRequest { Prompt = "Stats query", Model = "gpt-4", Temperature = 0 }); // Hit
        await _sut.GetAsync(new ResponseCacheRequest { Prompt = "Unknown", Model = "gpt-4", Temperature = 0 }); // Miss

        // Act
        var stats = await _sut.GetStatisticsAsync();

        // Assert
        stats.IsSuccess.Should().BeTrue();
        stats.Value.TotalEntries.Should().BeGreaterOrEqualTo(1);
        stats.Value.TotalHits.Should().BeGreaterOrEqualTo(2);
        stats.Value.TotalMisses.Should().BeGreaterOrEqualTo(1);
        stats.Value.TotalTokensCached.Should().BeGreaterOrEqualTo(100);
        stats.Value.ApiCallsSaved.Should().BeGreaterOrEqualTo(2);
        stats.Value.TotalLatencySavedMs.Should().BeGreaterOrEqualTo(400);
        stats.Value.TotalCostSavings.Should().BeGreaterOrEqualTo(0.04m);
    }

    [Fact]
    public async Task GetStatisticsAsync_TracksEntriesByModel()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry { Prompt = "A", Response = "R", Model = "gpt-4" });
        await _sut.SetAsync(new ResponseCacheEntry { Prompt = "B", Response = "R", Model = "gpt-4" });
        await _sut.SetAsync(new ResponseCacheEntry { Prompt = "C", Response = "R", Model = "claude-3" });

        // Act
        var stats = await _sut.GetStatisticsAsync();

        // Assert
        stats.Value.EntriesByModel.Should().ContainKey("gpt-4");
        stats.Value.EntriesByModel.Should().ContainKey("claude-3");
        stats.Value.EntriesByModel["gpt-4"].Should().Be(2);
        stats.Value.EntriesByModel["claude-3"].Should().Be(1);
    }

    #endregion

    #region TTL

    [Fact]
    public async Task GetAsync_ReturnsTtl()
    {
        // Arrange
        await _sut.SetAsync(new ResponseCacheEntry
        {
            Prompt = "TTL test",
            Response = "TTL response",
            Model = "gpt-4",
            Temperature = 0,
            TtlSeconds = 3600
        });

        // Act
        var result = await _sut.GetAsync(new ResponseCacheRequest
        {
            Prompt = "TTL test",
            Model = "gpt-4",
            Temperature = 0
        });

        // Assert
        result.Value.IsHit.Should().BeTrue();
        result.Value.TtlSeconds.Should().BeGreaterThan(0);
        result.Value.CachedAt.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetAsync_WithNullPrompt_ReturnsFailure()
    {
        // Arrange
        var request = new ResponseCacheRequest
        {
            Prompt = null!,
            Model = "gpt-4"
        };

        // Act
        var result = await _sut.GetAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WithEmptyModel_ReturnsFailure()
    {
        // Arrange
        var request = new ResponseCacheRequest
        {
            Prompt = "Test",
            Model = ""
        };

        // Act
        var result = await _sut.GetAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_WithEmptyResponse_Succeeds()
    {
        // Empty responses are valid (e.g., when model returns nothing)
        var entry = new ResponseCacheEntry
        {
            Prompt = "Empty test",
            Response = "",
            Model = "gpt-4"
        };

        // Act
        var result = await _sut.SetAsync(entry);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
