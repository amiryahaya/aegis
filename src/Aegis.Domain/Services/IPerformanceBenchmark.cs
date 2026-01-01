using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Performance benchmarking service for load testing and performance analysis
/// </summary>
public interface IPerformanceBenchmark
{
    /// <summary>
    /// Run a performance benchmark with specified configuration
    /// </summary>
    Task<Result<BenchmarkResult>> RunBenchmarkAsync(
        BenchmarkConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a quick latency test
    /// </summary>
    Task<Result<LatencyResult>> MeasureLatencyAsync(
        LatencyTestConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a throughput test
    /// </summary>
    Task<Result<ThroughputResult>> MeasureThroughputAsync(
        ThroughputTestConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a load test with ramping users
    /// </summary>
    Task<Result<LoadTestResult>> RunLoadTestAsync(
        LoadTestConfig config,
        IProgress<LoadTestProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get historical benchmark results
    /// </summary>
    Task<Result<List<BenchmarkSummary>>> GetBenchmarkHistoryAsync(
        Guid? workspaceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare two benchmark runs
    /// </summary>
    Task<Result<BenchmarkComparison>> CompareBenchmarksAsync(
        Guid baselineId,
        Guid currentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get performance recommendations based on benchmark results
    /// </summary>
    Task<Result<List<PerformanceRecommendation>>> GetRecommendationsAsync(
        Guid benchmarkId,
        CancellationToken cancellationToken = default);
}

#region Configuration Types

/// <summary>
/// Configuration for a benchmark run
/// </summary>
public record BenchmarkConfig
{
    public string Name { get; init; } = "Benchmark";
    public string? Description { get; init; }
    public Guid? WorkspaceId { get; init; }

    // Test parameters
    public required List<BenchmarkScenario> Scenarios { get; init; }
    public int WarmupIterations { get; init; } = 5;
    public int MeasurementIterations { get; init; } = 100;
    public int ConcurrentUsers { get; init; } = 1;
    public TimeSpan? Duration { get; init; }

    // Options
    public bool CollectMemoryMetrics { get; init; } = true;
    public bool CollectCpuMetrics { get; init; } = true;
    public bool CollectGCMetrics { get; init; } = true;
}

/// <summary>
/// A benchmark scenario to test
/// </summary>
public record BenchmarkScenario
{
    public required string Name { get; init; }
    public required BenchmarkType Type { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
    public int Weight { get; init; } = 1;
}

/// <summary>
/// Types of benchmarks
/// </summary>
public enum BenchmarkType
{
    Query,
    Embedding,
    DocumentIngestion,
    Search,
    LLMInference,
    CacheHit,
    CacheMiss,
    EndToEnd
}

/// <summary>
/// Configuration for latency test
/// </summary>
public record LatencyTestConfig
{
    public required BenchmarkType Type { get; init; }
    public int Iterations { get; init; } = 100;
    public int WarmupIterations { get; init; } = 10;
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Configuration for throughput test
/// </summary>
public record ThroughputTestConfig
{
    public required BenchmarkType Type { get; init; }
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(30);
    public int ConcurrentWorkers { get; init; } = 10;
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Configuration for load test
/// </summary>
public record LoadTestConfig
{
    public string Name { get; init; } = "Load Test";
    public required List<BenchmarkScenario> Scenarios { get; init; }

    // Ramping configuration
    public int StartUsers { get; init; } = 1;
    public int MaxUsers { get; init; } = 100;
    public TimeSpan RampUpDuration { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan SteadyStateDuration { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan RampDownDuration { get; init; } = TimeSpan.FromMinutes(2);

    // Thresholds
    public double? MaxP95LatencyMs { get; init; }
    public double? MaxErrorRate { get; init; }
    public double? MinThroughput { get; init; }
}

#endregion

#region Result Types

/// <summary>
/// Complete benchmark result
/// </summary>
public record BenchmarkResult
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? WorkspaceId { get; init; }

    // Timing
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    // Results by scenario
    public List<ScenarioResult> ScenarioResults { get; init; } = new();

    // Aggregate metrics
    public required PerformanceMetrics AggregateMetrics { get; init; }

    // Resource usage
    public ResourceMetrics? ResourceMetrics { get; init; }

    // Status
    public BenchmarkStatus Status { get; init; }
    public List<string>? Errors { get; init; }
}

/// <summary>
/// Result for a single scenario
/// </summary>
public record ScenarioResult
{
    public required string ScenarioName { get; init; }
    public required BenchmarkType Type { get; init; }
    public int TotalIterations { get; init; }
    public int SuccessfulIterations { get; init; }
    public int FailedIterations { get; init; }
    public required PerformanceMetrics Metrics { get; init; }
}

/// <summary>
/// Performance metrics
/// </summary>
public record PerformanceMetrics
{
    // Latency (in milliseconds)
    public double MinLatency { get; init; }
    public double MaxLatency { get; init; }
    public double MeanLatency { get; init; }
    public double MedianLatency { get; init; }
    public double P90Latency { get; init; }
    public double P95Latency { get; init; }
    public double P99Latency { get; init; }
    public double StdDevLatency { get; init; }

    // Throughput
    public double RequestsPerSecond { get; init; }
    public double OperationsPerSecond { get; init; }

    // Success/Error rates
    public double SuccessRate { get; init; }
    public double ErrorRate { get; init; }

    // Raw data for percentile calculations
    public List<double>? LatencyHistogram { get; init; }
}

/// <summary>
/// Resource utilization metrics
/// </summary>
public record ResourceMetrics
{
    // Memory
    public long PeakMemoryBytes { get; init; }
    public long AverageMemoryBytes { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }

    // CPU
    public double PeakCpuPercent { get; init; }
    public double AverageCpuPercent { get; init; }

    // Thread pool
    public int PeakThreadCount { get; init; }
    public int PeakWorkerThreads { get; init; }
    public int PeakCompletionPortThreads { get; init; }
}

/// <summary>
/// Latency test result
/// </summary>
public record LatencyResult
{
    public required BenchmarkType Type { get; init; }
    public int Iterations { get; init; }
    public required PerformanceMetrics Metrics { get; init; }
    public DateTimeOffset TestedAt { get; init; }
}

/// <summary>
/// Throughput test result
/// </summary>
public record ThroughputResult
{
    public required BenchmarkType Type { get; init; }
    public TimeSpan Duration { get; init; }
    public int TotalOperations { get; init; }
    public int SuccessfulOperations { get; init; }
    public double OperationsPerSecond { get; init; }
    public double BytesPerSecond { get; init; }
    public required PerformanceMetrics Metrics { get; init; }
    public DateTimeOffset TestedAt { get; init; }
}

/// <summary>
/// Load test result
/// </summary>
public record LoadTestResult
{
    public Guid Id { get; init; }
    public required string Name { get; init; }

    // Timing
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    // User metrics
    public int PeakConcurrentUsers { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }

    // Performance over time
    public List<LoadTestDataPoint> Timeline { get; init; } = new();

    // Aggregate metrics
    public required PerformanceMetrics AggregateMetrics { get; init; }

    // Threshold results
    public bool PassedThresholds { get; init; }
    public List<ThresholdResult> ThresholdResults { get; init; } = new();

    // Errors breakdown
    public Dictionary<string, int> ErrorsByType { get; init; } = new();
}

/// <summary>
/// A data point in load test timeline
/// </summary>
public record LoadTestDataPoint
{
    public DateTimeOffset Timestamp { get; init; }
    public int ActiveUsers { get; init; }
    public double RequestsPerSecond { get; init; }
    public double P95LatencyMs { get; init; }
    public double ErrorRate { get; init; }
}

/// <summary>
/// Load test progress for reporting
/// </summary>
public record LoadTestProgress
{
    public int CurrentUsers { get; init; }
    public int TotalRequests { get; init; }
    public double CurrentRps { get; init; }
    public double CurrentP95Ms { get; init; }
    public double ErrorRate { get; init; }
    public string Phase { get; init; } = "Running";
    public double ProgressPercent { get; init; }
}

/// <summary>
/// Threshold result
/// </summary>
public record ThresholdResult
{
    public required string ThresholdName { get; init; }
    public double ThresholdValue { get; init; }
    public double ActualValue { get; init; }
    public bool Passed { get; init; }
}

/// <summary>
/// Benchmark status
/// </summary>
public enum BenchmarkStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

#endregion

#region History and Comparison

/// <summary>
/// Summary of a benchmark for history listing
/// </summary>
public record BenchmarkSummary
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? WorkspaceId { get; init; }
    public DateTimeOffset RunAt { get; init; }
    public TimeSpan Duration { get; init; }
    public BenchmarkStatus Status { get; init; }
    public double MeanLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double RequestsPerSecond { get; init; }
    public double SuccessRate { get; init; }
}

/// <summary>
/// Comparison between two benchmark runs
/// </summary>
public record BenchmarkComparison
{
    public required BenchmarkSummary Baseline { get; init; }
    public required BenchmarkSummary Current { get; init; }

    // Changes (positive = improvement, negative = regression)
    public double LatencyChangePercent { get; init; }
    public double ThroughputChangePercent { get; init; }
    public double SuccessRateChangePercent { get; init; }

    // Assessment
    public ComparisonResult OverallResult { get; init; }
    public List<string> Insights { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Overall comparison result
/// </summary>
public enum ComparisonResult
{
    Improved,
    NoChange,
    Regressed,
    Mixed
}

#endregion

#region Recommendations

/// <summary>
/// Performance recommendation
/// </summary>
public record PerformanceRecommendation
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required RecommendationPriority Priority { get; init; }
    public required RecommendationCategory Category { get; init; }
    public double? EstimatedImprovementPercent { get; init; }
    public string? ImplementationGuide { get; init; }
}

/// <summary>
/// Recommendation priority
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Recommendation category
/// </summary>
public enum RecommendationCategory
{
    Caching,
    Indexing,
    QueryOptimization,
    ResourceScaling,
    Configuration,
    Architecture
}

#endregion

#region Error Definitions

public static class PerformanceBenchmarkErrors
{
    public static Error BenchmarkFailed => Error.Internal(
        "PerformanceBenchmark.Failed",
        "Benchmark execution failed");

    public static Error BenchmarkNotFound => Error.NotFound(
        "PerformanceBenchmark.NotFound",
        "Benchmark not found");

    public static Error InvalidConfig => Error.Validation(
        "PerformanceBenchmark.InvalidConfig",
        "Invalid benchmark configuration");

    public static Error NoScenarios => Error.Validation(
        "PerformanceBenchmark.NoScenarios",
        "No scenarios specified for benchmark");

    public static Error Cancelled => Error.Internal(
        "PerformanceBenchmark.Cancelled",
        "Benchmark was cancelled");
}

#endregion
