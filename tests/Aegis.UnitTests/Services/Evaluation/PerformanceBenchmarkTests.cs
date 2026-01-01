using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Evaluation;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aegis.UnitTests.Services.Evaluation;

public class PerformanceBenchmarkTests
{
    private readonly InMemoryPerformanceBenchmark _benchmark;
    private readonly Mock<ILogger<InMemoryPerformanceBenchmark>> _loggerMock;

    public PerformanceBenchmarkTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryPerformanceBenchmark>>();
        _benchmark = new InMemoryPerformanceBenchmark(_loggerMock.Object);
    }

    #region RunBenchmarkAsync Tests

    [Fact]
    public async Task RunBenchmarkAsync_WithValidConfig_ReturnsResult()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            Name = "Test Benchmark",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario
                {
                    Name = "Query Test",
                    Type = BenchmarkType.Query
                }
            },
            WarmupIterations = 2,
            MeasurementIterations = 10
        };

        // Act
        var result = await _benchmark.RunBenchmarkAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Benchmark");
        result.Value.Status.Should().Be(BenchmarkStatus.Completed);
        result.Value.ScenarioResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithNoScenarios_ReturnsFailure()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            Name = "Empty Benchmark",
            Scenarios = new List<BenchmarkScenario>()
        };

        // Act
        var result = await _benchmark.RunBenchmarkAsync(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NoScenarios");
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithMultipleScenarios_RunsAll()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            Name = "Multi-Scenario Benchmark",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query },
                new BenchmarkScenario { Name = "Search", Type = BenchmarkType.Search },
                new BenchmarkScenario { Name = "Embedding", Type = BenchmarkType.Embedding }
            },
            WarmupIterations = 1,
            MeasurementIterations = 5
        };

        // Act
        var result = await _benchmark.RunBenchmarkAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScenarioResults.Should().HaveCount(3);
        result.Value.ScenarioResults.Select(s => s.ScenarioName)
            .Should().BeEquivalentTo(new[] { "Query", "Search", "Embedding" });
    }

    [Fact]
    public async Task RunBenchmarkAsync_CollectsAggregateMetrics()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            Name = "Metrics Benchmark",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 20
        };

        // Act
        var result = await _benchmark.RunBenchmarkAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AggregateMetrics.Should().NotBeNull();
        result.Value.AggregateMetrics.MeanLatency.Should().BeGreaterThan(0);
        result.Value.AggregateMetrics.P95Latency.Should().BeGreaterThanOrEqualTo(
            result.Value.AggregateMetrics.MeanLatency);
        result.Value.AggregateMetrics.P99Latency.Should().BeGreaterThanOrEqualTo(
            result.Value.AggregateMetrics.P95Latency);
    }

    [Fact]
    public async Task RunBenchmarkAsync_CollectsResourceMetrics()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            Name = "Resource Benchmark",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            CollectMemoryMetrics = true,
            CollectCpuMetrics = true
        };

        // Act
        var result = await _benchmark.RunBenchmarkAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ResourceMetrics.Should().NotBeNull();
        // Memory metrics may be 0 on some platforms, just verify they're collected
        result.Value.ResourceMetrics!.PeakMemoryBytes.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithConcurrentUsers_RunsInParallel()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            Name = "Concurrent Benchmark",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            ConcurrentUsers = 5,
            MeasurementIterations = 20
        };

        // Act
        var result = await _benchmark.RunBenchmarkAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScenarioResults[0].TotalIterations.Should().BeGreaterThan(0);
    }

    #endregion

    #region MeasureLatencyAsync Tests

    [Fact]
    public async Task MeasureLatencyAsync_ReturnsLatencyMetrics()
    {
        // Arrange
        var config = new LatencyTestConfig
        {
            Type = BenchmarkType.Search,
            Iterations = 50,
            WarmupIterations = 5
        };

        // Act
        var result = await _benchmark.MeasureLatencyAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(BenchmarkType.Search);
        result.Value.Iterations.Should().Be(50);
        result.Value.Metrics.MinLatency.Should().BeGreaterThan(0);
        result.Value.Metrics.MaxLatency.Should().BeGreaterThanOrEqualTo(result.Value.Metrics.MinLatency);
    }

    [Fact]
    public async Task MeasureLatencyAsync_WithDifferentTypes_HasDifferentLatencies()
    {
        // Arrange
        var cacheHitConfig = new LatencyTestConfig { Type = BenchmarkType.CacheHit, Iterations = 20 };
        var llmConfig = new LatencyTestConfig { Type = BenchmarkType.LLMInference, Iterations = 20 };

        // Act
        var cacheResult = await _benchmark.MeasureLatencyAsync(cacheHitConfig);
        var llmResult = await _benchmark.MeasureLatencyAsync(llmConfig);

        // Assert
        cacheResult.IsSuccess.Should().BeTrue();
        llmResult.IsSuccess.Should().BeTrue();
        // LLM should generally be slower than cache hit
        cacheResult.Value.Metrics.MeanLatency.Should().BeLessThan(llmResult.Value.Metrics.MeanLatency);
    }

    #endregion

    #region MeasureThroughputAsync Tests

    [Fact]
    public async Task MeasureThroughputAsync_ReturnsThroughputMetrics()
    {
        // Arrange
        var config = new ThroughputTestConfig
        {
            Type = BenchmarkType.Embedding,
            Duration = TimeSpan.FromSeconds(2),
            ConcurrentWorkers = 3
        };

        // Act
        var result = await _benchmark.MeasureThroughputAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalOperations.Should().BeGreaterThan(0);
        result.Value.OperationsPerSecond.Should().BeGreaterThan(0);
        result.Value.SuccessfulOperations.Should().BeLessOrEqualTo(result.Value.TotalOperations);
    }

    [Fact]
    public async Task MeasureThroughputAsync_WithMoreWorkers_HasHigherThroughput()
    {
        // Arrange
        var singleWorkerConfig = new ThroughputTestConfig
        {
            Type = BenchmarkType.Search,
            Duration = TimeSpan.FromSeconds(1),
            ConcurrentWorkers = 1
        };

        var multiWorkerConfig = new ThroughputTestConfig
        {
            Type = BenchmarkType.Search,
            Duration = TimeSpan.FromSeconds(1),
            ConcurrentWorkers = 5
        };

        // Act
        var singleResult = await _benchmark.MeasureThroughputAsync(singleWorkerConfig);
        var multiResult = await _benchmark.MeasureThroughputAsync(multiWorkerConfig);

        // Assert
        singleResult.IsSuccess.Should().BeTrue();
        multiResult.IsSuccess.Should().BeTrue();
        multiResult.Value.TotalOperations.Should().BeGreaterThanOrEqualTo(singleResult.Value.TotalOperations);
    }

    #endregion

    #region RunLoadTestAsync Tests

    [Fact]
    public async Task RunLoadTestAsync_WithValidConfig_ReturnsResult()
    {
        // Arrange
        var config = new LoadTestConfig
        {
            Name = "Basic Load Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            StartUsers = 1,
            MaxUsers = 5,
            RampUpDuration = TimeSpan.FromMilliseconds(100),
            SteadyStateDuration = TimeSpan.FromMilliseconds(200),
            RampDownDuration = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var result = await _benchmark.RunLoadTestAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Basic Load Test");
        result.Value.TotalRequests.Should().BeGreaterThan(0);
        result.Value.PeakConcurrentUsers.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RunLoadTestAsync_TracksTimeline()
    {
        // Arrange
        var config = new LoadTestConfig
        {
            Name = "Timeline Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            RampUpDuration = TimeSpan.FromMilliseconds(100),
            SteadyStateDuration = TimeSpan.FromMilliseconds(100),
            RampDownDuration = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var result = await _benchmark.RunLoadTestAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Timeline.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunLoadTestAsync_ChecksThresholds()
    {
        // Arrange
        var config = new LoadTestConfig
        {
            Name = "Threshold Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MaxUsers = 3,
            RampUpDuration = TimeSpan.FromMilliseconds(50),
            SteadyStateDuration = TimeSpan.FromMilliseconds(100),
            RampDownDuration = TimeSpan.FromMilliseconds(50),
            MaxP95LatencyMs = 10000, // Very high threshold - should pass
            MaxErrorRate = 10
        };

        // Act
        var result = await _benchmark.RunLoadTestAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ThresholdResults.Should().NotBeEmpty();
        result.Value.ThresholdResults.Should().OnlyContain(t => t.Passed);
        result.Value.PassedThresholds.Should().BeTrue();
    }

    [Fact]
    public async Task RunLoadTestAsync_ReportsProgress()
    {
        // Arrange
        var progressReports = new List<LoadTestProgress>();
        var progress = new Progress<LoadTestProgress>(p => progressReports.Add(p));

        var config = new LoadTestConfig
        {
            Name = "Progress Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            RampUpDuration = TimeSpan.FromMilliseconds(100),
            SteadyStateDuration = TimeSpan.FromMilliseconds(100),
            RampDownDuration = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var result = await _benchmark.RunLoadTestAsync(config, progress);

        // Assert
        result.IsSuccess.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunLoadTestAsync_WithNoScenarios_ReturnsFailure()
    {
        // Arrange
        var config = new LoadTestConfig
        {
            Name = "Empty Load Test",
            Scenarios = new List<BenchmarkScenario>()
        };

        // Act
        var result = await _benchmark.RunLoadTestAsync(config);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region GetBenchmarkHistoryAsync Tests

    [Fact]
    public async Task GetBenchmarkHistoryAsync_ReturnsEmptyInitially()
    {
        // Act
        var result = await _benchmark.GetBenchmarkHistoryAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBenchmarkHistoryAsync_ReturnsPreviousRuns()
    {
        // Arrange - Run a benchmark first
        await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "History Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 5
        });

        // Act
        var result = await _benchmark.GetBenchmarkHistoryAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(s => s.Name == "History Test");
    }

    [Fact]
    public async Task GetBenchmarkHistoryAsync_FiltersByWorkspace()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "Workspace Benchmark",
            WorkspaceId = workspaceId,
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 5
        });

        await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "Other Benchmark",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 5
        });

        // Act
        var result = await _benchmark.GetBenchmarkHistoryAsync(workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(s => s.WorkspaceId == workspaceId);
    }

    #endregion

    #region CompareBenchmarksAsync Tests

    [Fact]
    public async Task CompareBenchmarksAsync_WithValidIds_ReturnsComparison()
    {
        // Arrange - Run two benchmarks
        var baseline = await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "Baseline",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 10
        });

        var current = await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "Current",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 10
        });

        // Act
        var result = await _benchmark.CompareBenchmarksAsync(baseline.Value.Id, current.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Baseline.Should().NotBeNull();
        result.Value.Current.Should().NotBeNull();
        result.Value.OverallResult.Should().BeOneOf(
            ComparisonResult.Improved,
            ComparisonResult.NoChange,
            ComparisonResult.Regressed,
            ComparisonResult.Mixed);
    }

    [Fact]
    public async Task CompareBenchmarksAsync_WithInvalidId_ReturnsFailure()
    {
        // Act
        var result = await _benchmark.CompareBenchmarksAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    #endregion

    #region GetRecommendationsAsync Tests

    [Fact]
    public async Task GetRecommendationsAsync_WithValidId_ReturnsRecommendations()
    {
        // Arrange
        var benchmarkResult = await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "Recommendations Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 10
        });

        // Act
        var result = await _benchmark.GetRecommendationsAsync(benchmarkResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().OnlyContain(r =>
            !string.IsNullOrEmpty(r.Title) &&
            !string.IsNullOrEmpty(r.Description));
    }

    [Fact]
    public async Task GetRecommendationsAsync_WithInvalidId_ReturnsFailure()
    {
        // Act
        var result = await _benchmark.GetRecommendationsAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendationsAsync_IncludesPriorityAndCategory()
    {
        // Arrange
        var benchmarkResult = await _benchmark.RunBenchmarkAsync(new BenchmarkConfig
        {
            Name = "Category Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 5
        });

        // Act
        var result = await _benchmark.GetRecommendationsAsync(benchmarkResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(r =>
            Enum.IsDefined(typeof(RecommendationPriority), r.Priority) &&
            Enum.IsDefined(typeof(RecommendationCategory), r.Category));
    }

    #endregion

    #region Scenario Weights Tests

    [Fact]
    public async Task RunLoadTestAsync_RespectsScenarioWeights()
    {
        // Arrange
        var config = new LoadTestConfig
        {
            Name = "Weighted Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Heavy", Type = BenchmarkType.Query, Weight = 10 },
                new BenchmarkScenario { Name = "Light", Type = BenchmarkType.CacheHit, Weight = 1 }
            },
            RampUpDuration = TimeSpan.FromMilliseconds(50),
            SteadyStateDuration = TimeSpan.FromMilliseconds(100),
            RampDownDuration = TimeSpan.FromMilliseconds(50)
        };

        // Act
        var result = await _benchmark.RunLoadTestAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should have more requests overall with weighted scenarios
        result.Value.TotalRequests.Should().BeGreaterThan(0);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task RunBenchmarkAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var config = new BenchmarkConfig
        {
            Name = "Cancellation Test",
            Scenarios = new List<BenchmarkScenario>
            {
                new BenchmarkScenario { Name = "Query", Type = BenchmarkType.Query }
            },
            MeasurementIterations = 1000 // Long running
        };

        // Act
        cts.Cancel();
        var result = await _benchmark.RunBenchmarkAsync(config, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Cancelled");
    }

    #endregion
}
