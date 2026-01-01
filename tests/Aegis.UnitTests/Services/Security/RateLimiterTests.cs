using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Security;

public class RateLimiterTests
{
    private readonly ILogger<InMemoryRateLimiter> _logger;
    private readonly InMemoryRateLimiter _sut;

    public RateLimiterTests()
    {
        _logger = Substitute.For<ILogger<InMemoryRateLimiter>>();
        var config = new RateLimitConfig
        {
            Name = "default",
            Limits = new List<WindowLimit>
            {
                new WindowLimit { WindowSeconds = 60, MaxRequests = 10 }, // 10 per minute
                new WindowLimit { WindowSeconds = 3600, MaxRequests = 100 } // 100 per hour
            }
        };
        _sut = new InMemoryRateLimiter(_logger, config);
    }

    #region Check and Record

    [Fact]
    public async Task CheckAsync_WithNoHistory_AllowsRequest()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-1",
            LimitType = RateLimitType.User
        };

        // Act
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAllowed.Should().BeTrue();
        result.Value.RemainingRequests.Should().Be(10);
    }

    [Fact]
    public async Task RecordRequestAsync_IncrementsCounter()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-2",
            LimitType = RateLimitType.User
        };

        // Act
        var result = await _sut.RecordRequestAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentCount.Should().Be(1);
        result.Value.RemainingRequests.Should().Be(9);
    }

    [Fact]
    public async Task CheckAsync_AfterMultipleRequests_UpdatesRemaining()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-3",
            LimitType = RateLimitType.User
        };

        // Record 5 requests
        for (int i = 0; i < 5; i++)
        {
            await _sut.RecordRequestAsync(request);
        }

        // Act
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAllowed.Should().BeTrue();
        result.Value.CurrentCount.Should().Be(5);
        result.Value.RemainingRequests.Should().Be(5);
    }

    [Fact]
    public async Task CheckAsync_WhenLimitExceeded_DeniesRequest()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-4",
            LimitType = RateLimitType.User
        };

        // Exhaust the limit
        for (int i = 0; i < 10; i++)
        {
            await _sut.RecordRequestAsync(request);
        }

        // Act
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAllowed.Should().BeFalse();
        result.Value.RemainingRequests.Should().Be(0);
        result.Value.RetryAfterSeconds.Should().BeGreaterThan(0);
    }

    #endregion

    #region Different Limit Types

    [Fact]
    public async Task CheckAsync_DifferentUsers_IndependentLimits()
    {
        // Arrange
        var user1 = new RateLimitRequest { Identifier = "user-a", LimitType = RateLimitType.User };
        var user2 = new RateLimitRequest { Identifier = "user-b", LimitType = RateLimitType.User };

        // Exhaust user1's limit
        for (int i = 0; i < 10; i++)
        {
            await _sut.RecordRequestAsync(user1);
        }

        // Act
        var result = await _sut.CheckAsync(user2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAllowed.Should().BeTrue();
        result.Value.RemainingRequests.Should().Be(10);
    }

    [Fact]
    public async Task CheckAsync_SameUserDifferentResource_IndependentLimits()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            Name = "resource",
            Limits = new List<WindowLimit>
            {
                new WindowLimit { WindowSeconds = 60, MaxRequests = 5 }
            }
        };
        var limiter = new InMemoryRateLimiter(_logger, config);

        var resource1 = new RateLimitRequest { Identifier = "user-x", Resource = "api/query", LimitType = RateLimitType.Resource };
        var resource2 = new RateLimitRequest { Identifier = "user-x", Resource = "api/upload", LimitType = RateLimitType.Resource };

        // Exhaust resource1 limit
        for (int i = 0; i < 5; i++)
        {
            await limiter.RecordRequestAsync(resource1);
        }

        // Act
        var result = await limiter.CheckAsync(resource2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAllowed.Should().BeTrue();
    }

    #endregion

    #region Weighted Requests

    [Fact]
    public async Task RecordRequestAsync_WithCost_AppliesWeight()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-weighted",
            LimitType = RateLimitType.User,
            Cost = 3 // Counts as 3 requests
        };

        // Act
        await _sut.RecordRequestAsync(request);
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentCount.Should().Be(3);
        result.Value.RemainingRequests.Should().Be(7);
    }

    [Fact]
    public async Task CheckAsync_WithHighCost_ExceedsLimitFaster()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-high-cost",
            LimitType = RateLimitType.User,
            Cost = 5
        };

        // Record twice with cost 5 each = 10 total
        await _sut.RecordRequestAsync(request);
        await _sut.RecordRequestAsync(request);

        // Act
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAllowed.Should().BeFalse();
    }

    #endregion

    #region Status

    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentStatus()
    {
        // Arrange
        var identifier = "user-status";
        var request = new RateLimitRequest { Identifier = identifier, LimitType = RateLimitType.User };

        await _sut.RecordRequestAsync(request);
        await _sut.RecordRequestAsync(request);
        await _sut.RecordRequestAsync(request);

        // Act
        var result = await _sut.GetStatusAsync(identifier, RateLimitType.User);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Identifier.Should().Be(identifier);
        result.Value.IsBlocked.Should().BeFalse();
        result.Value.Windows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_WhenBlocked_ShowsBlockInfo()
    {
        // Arrange
        var identifier = "user-blocked";
        var request = new RateLimitRequest { Identifier = identifier, LimitType = RateLimitType.User };

        // Exhaust the limit
        for (int i = 0; i < 10; i++)
        {
            await _sut.RecordRequestAsync(request);
        }

        // Act
        var result = await _sut.GetStatusAsync(identifier, RateLimitType.User);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsBlocked.Should().BeTrue();
        result.Value.BlockExpiresAt.Should().NotBeNull();
    }

    #endregion

    #region Reset

    [Fact]
    public async Task ResetAsync_ClearsLimits()
    {
        // Arrange
        var identifier = "user-reset";
        var request = new RateLimitRequest { Identifier = identifier, LimitType = RateLimitType.User };

        // Record some requests
        for (int i = 0; i < 5; i++)
        {
            await _sut.RecordRequestAsync(request);
        }

        // Act
        var resetResult = await _sut.ResetAsync(identifier, RateLimitType.User);
        var checkResult = await _sut.CheckAsync(request);

        // Assert
        resetResult.IsSuccess.Should().BeTrue();
        checkResult.Value.CurrentCount.Should().Be(0);
        checkResult.Value.RemainingRequests.Should().Be(10);
    }

    #endregion

    #region Headers

    [Fact]
    public async Task CheckAsync_ReturnsRateLimitHeaders()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-headers",
            LimitType = RateLimitType.User
        };

        await _sut.RecordRequestAsync(request);

        // Act
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Headers.Should().ContainKey("X-RateLimit-Limit");
        result.Value.Headers.Should().ContainKey("X-RateLimit-Remaining");
        result.Value.Headers.Should().ContainKey("X-RateLimit-Reset");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CheckAsync_WithEmptyIdentifier_ReturnsFailure()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "",
            LimitType = RateLimitType.User
        };

        // Act
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RecordRequestAsync_WithZeroCost_DoesNotCount()
    {
        // Arrange
        var request = new RateLimitRequest
        {
            Identifier = "user-zero-cost",
            LimitType = RateLimitType.User,
            Cost = 0
        };

        // Act
        await _sut.RecordRequestAsync(request);
        var result = await _sut.CheckAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentCount.Should().Be(0);
    }

    #endregion
}
