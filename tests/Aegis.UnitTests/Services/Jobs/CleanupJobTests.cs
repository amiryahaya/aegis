using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Jobs;

public class CleanupJobTests
{
    private readonly ISemanticCache _semanticCache;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly IResponseCache _responseCache;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CleanupJob> _logger;
    private readonly CleanupJob _sut;

    public CleanupJobTests()
    {
        _semanticCache = Substitute.For<ISemanticCache>();
        _embeddingCache = Substitute.For<IEmbeddingCache>();
        _responseCache = Substitute.For<IResponseCache>();
        _auditLogService = Substitute.For<IAuditLogService>();
        _logger = Substitute.For<ILogger<CleanupJob>>();

        _sut = new CleanupJob(
            _semanticCache,
            _embeddingCache,
            _responseCache,
            _auditLogService,
            _logger);
    }

    [Fact]
    public async Task CleanupAsync_ShouldEvictExpiredCacheEntries()
    {
        // Arrange
        _semanticCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<CacheStatistics>.Success(new CacheStatistics { TotalEntries = 100 }));
        _semanticCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(10));

        _embeddingCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingCacheStatistics>.Success(new EmbeddingCacheStatistics { TotalEntries = 200 }));
        _embeddingCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(20));

        _responseCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<ResponseCacheStatistics>.Success(new ResponseCacheStatistics { TotalEntries = 50 }));
        _responseCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(5));

        _auditLogService.ArchiveLogsBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(100));

        // Act
        var result = await _sut.CleanupAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiredCacheEntriesRemoved.Should().Be(35); // 10 + 20 + 5
        result.Value.OldAuditLogsArchived.Should().Be(100);
    }

    [Fact]
    public async Task CleanupAsync_WhenCacheEvictionFails_ShouldContinue()
    {
        // Arrange
        _semanticCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<CacheStatistics>.Failure(Error.Internal("test", "error")));
        _embeddingCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingCacheStatistics>.Success(new EmbeddingCacheStatistics()));
        _embeddingCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(5));
        _responseCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<ResponseCacheStatistics>.Success(new ResponseCacheStatistics()));
        _responseCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(3));
        _auditLogService.ArchiveLogsBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));

        // Act
        var result = await _sut.CleanupAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiredCacheEntriesRemoved.Should().Be(8); // 5 + 3
    }

    [Fact]
    public async Task ExecuteAsync_WithJobId_ShouldExecuteCleanup()
    {
        // Arrange
        SetupSuccessfulCleanup();

        // Act & Assert (should not throw)
        await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        await _semanticCache.Received(1).GetStatsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_ShouldExecuteCleanup()
    {
        // Arrange
        SetupSuccessfulCleanup();

        // Act & Assert (should not throw)
        await _sut.ExecuteAsync(CancellationToken.None);

        await _auditLogService.Received(1).ArchiveLogsBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_WithNullSemanticCache_ShouldThrow()
    {
        // Act
        var act = () => new CleanupJob(null!, _embeddingCache, _responseCache, _auditLogService, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("semanticCache");
    }

    [Fact]
    public void Constructor_WithNullEmbeddingCache_ShouldThrow()
    {
        // Act
        var act = () => new CleanupJob(_semanticCache, null!, _responseCache, _auditLogService, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("embeddingCache");
    }

    [Fact]
    public void Constructor_WithNullResponseCache_ShouldThrow()
    {
        // Act
        var act = () => new CleanupJob(_semanticCache, _embeddingCache, null!, _auditLogService, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("responseCache");
    }

    [Fact]
    public void Constructor_WithNullAuditLogService_ShouldThrow()
    {
        // Act
        var act = () => new CleanupJob(_semanticCache, _embeddingCache, _responseCache, null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("auditLogService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new CleanupJob(_semanticCache, _embeddingCache, _responseCache, _auditLogService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private void SetupSuccessfulCleanup()
    {
        _semanticCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<CacheStatistics>.Success(new CacheStatistics()));
        _semanticCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        _embeddingCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<EmbeddingCacheStatistics>.Success(new EmbeddingCacheStatistics()));
        _embeddingCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        _responseCache.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<ResponseCacheStatistics>.Success(new ResponseCacheStatistics()));
        _responseCache.EvictExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        _auditLogService.ArchiveLogsBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
    }
}
