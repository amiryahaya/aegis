using System.Collections.Concurrent;
using System.Diagnostics;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Evaluation;

/// <summary>
/// In-memory implementation of performance benchmarking service
/// </summary>
public class InMemoryPerformanceBenchmark : IPerformanceBenchmark
{
    private readonly ILogger<InMemoryPerformanceBenchmark> _logger;
    private readonly ConcurrentDictionary<Guid, BenchmarkResult> _benchmarkHistory = new();
    private readonly ConcurrentDictionary<Guid, LoadTestResult> _loadTestHistory = new();

    // Simulated baseline latencies for different operations (in ms)
    private static readonly Dictionary<BenchmarkType, (double Mean, double StdDev)> _baseLatencies = new()
    {
        [BenchmarkType.Query] = (150, 50),
        [BenchmarkType.Embedding] = (25, 10),
        [BenchmarkType.DocumentIngestion] = (500, 150),
        [BenchmarkType.Search] = (50, 20),
        [BenchmarkType.LLMInference] = (800, 200),
        [BenchmarkType.CacheHit] = (2, 1),
        [BenchmarkType.CacheMiss] = (100, 30),
        [BenchmarkType.EndToEnd] = (1200, 300)
    };

    public InMemoryPerformanceBenchmark(ILogger<InMemoryPerformanceBenchmark> logger)
    {
        _logger = logger;
    }

    public async Task<Result<BenchmarkResult>> RunBenchmarkAsync(
        BenchmarkConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config.Scenarios == null || config.Scenarios.Count == 0)
        {
            return Result.Failure<BenchmarkResult>(PerformanceBenchmarkErrors.NoScenarios);
        }

        var startTime = DateTimeOffset.UtcNow;
        var scenarioResults = new List<ScenarioResult>();

        try
        {
            _logger.LogInformation("Starting benchmark '{Name}' with {ScenarioCount} scenarios",
                config.Name, config.Scenarios.Count);

            foreach (var scenario in config.Scenarios)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure<BenchmarkResult>(PerformanceBenchmarkErrors.Cancelled);
                }

                var scenarioResult = await RunScenarioAsync(
                    scenario,
                    config.WarmupIterations,
                    config.MeasurementIterations,
                    config.ConcurrentUsers,
                    cancellationToken);

                scenarioResults.Add(scenarioResult);
            }

            var aggregateMetrics = CalculateAggregateMetrics(scenarioResults);

            var result = new BenchmarkResult
            {
                Id = UuidGenerator.NewId(),
                Name = config.Name,
                WorkspaceId = config.WorkspaceId,
                StartedAt = startTime,
                CompletedAt = DateTimeOffset.UtcNow,
                ScenarioResults = scenarioResults,
                AggregateMetrics = aggregateMetrics,
                ResourceMetrics = CollectResourceMetrics(),
                Status = BenchmarkStatus.Completed
            };

            _benchmarkHistory[result.Id] = result;

            _logger.LogInformation(
                "Benchmark '{Name}' completed - Mean: {Mean:F2}ms, P95: {P95:F2}ms, RPS: {RPS:F2}",
                config.Name,
                aggregateMetrics.MeanLatency,
                aggregateMetrics.P95Latency,
                aggregateMetrics.RequestsPerSecond);

            return Result.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<BenchmarkResult>(PerformanceBenchmarkErrors.Cancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmark '{Name}' failed", config.Name);
            return Result.Failure<BenchmarkResult>(PerformanceBenchmarkErrors.BenchmarkFailed);
        }
    }

    public async Task<Result<LatencyResult>> MeasureLatencyAsync(
        LatencyTestConfig config,
        CancellationToken cancellationToken = default)
    {
        var latencies = new List<double>();

        // Warmup
        for (int i = 0; i < config.WarmupIterations; i++)
        {
            await SimulateOperationAsync(config.Type, cancellationToken);
        }

        // Measure
        for (int i = 0; i < config.Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await SimulateOperationAsync(config.Type, cancellationToken);
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        var metrics = CalculateMetrics(latencies);

        var result = new LatencyResult
        {
            Type = config.Type,
            Iterations = config.Iterations,
            Metrics = metrics,
            TestedAt = DateTimeOffset.UtcNow
        };

        return Result.Success(result);
    }

    public async Task<Result<ThroughputResult>> MeasureThroughputAsync(
        ThroughputTestConfig config,
        CancellationToken cancellationToken = default)
    {
        var latencies = new List<double>();
        var operations = 0;
        var successful = 0;
        var sw = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(config.Duration);

        var tasks = Enumerable.Range(0, config.ConcurrentWorkers)
            .Select(async _ =>
            {
                var localLatencies = new List<double>();
                var localOps = 0;
                var localSuccess = 0;

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var opSw = Stopwatch.StartNew();
                        await SimulateOperationAsync(config.Type, cts.Token);
                        opSw.Stop();
                        localLatencies.Add(opSw.Elapsed.TotalMilliseconds);
                        localOps++;
                        localSuccess++;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        localOps++;
                    }
                }

                return (localLatencies, localOps, localSuccess);
            })
            .ToList();

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        foreach (var (localLatencies, localOps, localSuccess) in results)
        {
            latencies.AddRange(localLatencies);
            operations += localOps;
            successful += localSuccess;
        }

        var metrics = CalculateMetrics(latencies);

        var result = new ThroughputResult
        {
            Type = config.Type,
            Duration = sw.Elapsed,
            TotalOperations = operations,
            SuccessfulOperations = successful,
            OperationsPerSecond = operations / sw.Elapsed.TotalSeconds,
            BytesPerSecond = operations * 1024 / sw.Elapsed.TotalSeconds, // Simulated
            Metrics = metrics,
            TestedAt = DateTimeOffset.UtcNow
        };

        return Result.Success(result);
    }

    public async Task<Result<LoadTestResult>> RunLoadTestAsync(
        LoadTestConfig config,
        IProgress<LoadTestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (config.Scenarios == null || config.Scenarios.Count == 0)
        {
            return Result.Failure<LoadTestResult>(PerformanceBenchmarkErrors.NoScenarios);
        }

        var startTime = DateTimeOffset.UtcNow;
        var timeline = new List<LoadTestDataPoint>();
        var allLatencies = new List<double>();
        var totalRequests = 0;
        var successfulRequests = 0;
        var failedRequests = 0;
        var errorsByType = new Dictionary<string, int>();
        var peakUsers = 0;

        var totalDuration = config.RampUpDuration + config.SteadyStateDuration + config.RampDownDuration;

        try
        {
            _logger.LogInformation("Starting load test '{Name}' with max {MaxUsers} users",
                config.Name, config.MaxUsers);

            var elapsed = TimeSpan.Zero;
            var interval = TimeSpan.FromSeconds(1);

            while (elapsed < totalDuration && !cancellationToken.IsCancellationRequested)
            {
                // Calculate current users based on phase
                var currentUsers = CalculateCurrentUsers(
                    elapsed, config.RampUpDuration, config.SteadyStateDuration, config.RampDownDuration,
                    config.StartUsers, config.MaxUsers);

                peakUsers = Math.Max(peakUsers, currentUsers);

                // Simulate requests for current interval
                var intervalLatencies = new List<double>();
                var intervalRequests = currentUsers * 2; // ~2 requests per user per second

                for (int i = 0; i < intervalRequests; i++)
                {
                    var scenario = SelectRandomScenario(config.Scenarios);
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        await SimulateOperationAsync(scenario.Type, cancellationToken);
                        sw.Stop();
                        intervalLatencies.Add(sw.Elapsed.TotalMilliseconds);
                        allLatencies.Add(sw.Elapsed.TotalMilliseconds);
                        successfulRequests++;
                    }
                    catch (Exception ex)
                    {
                        failedRequests++;
                        var errorType = ex.GetType().Name;
                        errorsByType[errorType] = errorsByType.GetValueOrDefault(errorType) + 1;
                    }
                    totalRequests++;
                }

                // Record timeline point
                var p95 = intervalLatencies.Count > 0 ? Percentile(intervalLatencies.OrderBy(l => l).ToList(), 95) : 0;
                var errorRate = intervalRequests > 0 ? (double)failedRequests / totalRequests * 100 : 0;

                timeline.Add(new LoadTestDataPoint
                {
                    Timestamp = startTime + elapsed,
                    ActiveUsers = currentUsers,
                    RequestsPerSecond = intervalRequests,
                    P95LatencyMs = p95,
                    ErrorRate = errorRate
                });

                // Report progress
                var progressPercent = elapsed.TotalSeconds / totalDuration.TotalSeconds * 100;
                var phase = GetPhase(elapsed, config.RampUpDuration, config.SteadyStateDuration);

                progress?.Report(new LoadTestProgress
                {
                    CurrentUsers = currentUsers,
                    TotalRequests = totalRequests,
                    CurrentRps = intervalRequests,
                    CurrentP95Ms = p95,
                    ErrorRate = errorRate,
                    Phase = phase,
                    ProgressPercent = progressPercent
                });

                elapsed += interval;
                await Task.Delay(10, cancellationToken); // Small delay to prevent tight loop
            }

            var aggregateMetrics = CalculateMetrics(allLatencies);

            // Check thresholds
            var thresholdResults = new List<ThresholdResult>();
            var passedThresholds = true;

            if (config.MaxP95LatencyMs.HasValue)
            {
                var passed = aggregateMetrics.P95Latency <= config.MaxP95LatencyMs;
                thresholdResults.Add(new ThresholdResult
                {
                    ThresholdName = "P95 Latency",
                    ThresholdValue = config.MaxP95LatencyMs.Value,
                    ActualValue = aggregateMetrics.P95Latency,
                    Passed = passed
                });
                passedThresholds &= passed;
            }

            if (config.MaxErrorRate.HasValue)
            {
                var errorRate = (double)failedRequests / totalRequests * 100;
                var passed = errorRate <= config.MaxErrorRate;
                thresholdResults.Add(new ThresholdResult
                {
                    ThresholdName = "Error Rate",
                    ThresholdValue = config.MaxErrorRate.Value,
                    ActualValue = errorRate,
                    Passed = passed
                });
                passedThresholds &= passed;
            }

            if (config.MinThroughput.HasValue)
            {
                var throughput = totalRequests / totalDuration.TotalSeconds;
                var passed = throughput >= config.MinThroughput;
                thresholdResults.Add(new ThresholdResult
                {
                    ThresholdName = "Min Throughput",
                    ThresholdValue = config.MinThroughput.Value,
                    ActualValue = throughput,
                    Passed = passed
                });
                passedThresholds &= passed;
            }

            var result = new LoadTestResult
            {
                Id = UuidGenerator.NewId(),
                Name = config.Name,
                StartedAt = startTime,
                CompletedAt = DateTimeOffset.UtcNow,
                PeakConcurrentUsers = peakUsers,
                TotalRequests = totalRequests,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests,
                Timeline = timeline,
                AggregateMetrics = aggregateMetrics,
                PassedThresholds = passedThresholds,
                ThresholdResults = thresholdResults,
                ErrorsByType = errorsByType
            };

            _loadTestHistory[result.Id] = result;

            _logger.LogInformation(
                "Load test '{Name}' completed - Total: {Total}, Failed: {Failed}, P95: {P95:F2}ms, Passed: {Passed}",
                config.Name, totalRequests, failedRequests, aggregateMetrics.P95Latency, passedThresholds);

            return Result.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<LoadTestResult>(PerformanceBenchmarkErrors.Cancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load test '{Name}' failed", config.Name);
            return Result.Failure<LoadTestResult>(PerformanceBenchmarkErrors.BenchmarkFailed);
        }
    }

    public Task<Result<List<BenchmarkSummary>>> GetBenchmarkHistoryAsync(
        Guid? workspaceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        from ??= DateTimeOffset.MinValue;
        to ??= DateTimeOffset.MaxValue;

        var summaries = _benchmarkHistory.Values
            .Where(b => (!workspaceId.HasValue || b.WorkspaceId == workspaceId) &&
                       b.StartedAt >= from && b.StartedAt <= to)
            .Select(b => new BenchmarkSummary
            {
                Id = b.Id,
                Name = b.Name,
                WorkspaceId = b.WorkspaceId,
                RunAt = b.StartedAt,
                Duration = b.Duration,
                Status = b.Status,
                MeanLatencyMs = b.AggregateMetrics.MeanLatency,
                P95LatencyMs = b.AggregateMetrics.P95Latency,
                RequestsPerSecond = b.AggregateMetrics.RequestsPerSecond,
                SuccessRate = b.AggregateMetrics.SuccessRate
            })
            .OrderByDescending(s => s.RunAt)
            .ToList();

        return Task.FromResult(Result.Success(summaries));
    }

    public Task<Result<BenchmarkComparison>> CompareBenchmarksAsync(
        Guid baselineId,
        Guid currentId,
        CancellationToken cancellationToken = default)
    {
        if (!_benchmarkHistory.TryGetValue(baselineId, out var baseline) ||
            !_benchmarkHistory.TryGetValue(currentId, out var current))
        {
            return Task.FromResult(Result.Failure<BenchmarkComparison>(
                PerformanceBenchmarkErrors.BenchmarkNotFound));
        }

        var latencyChange = (baseline.AggregateMetrics.MeanLatency - current.AggregateMetrics.MeanLatency) /
                           baseline.AggregateMetrics.MeanLatency * 100;
        var throughputChange = (current.AggregateMetrics.RequestsPerSecond - baseline.AggregateMetrics.RequestsPerSecond) /
                              baseline.AggregateMetrics.RequestsPerSecond * 100;
        var successRateChange = current.AggregateMetrics.SuccessRate - baseline.AggregateMetrics.SuccessRate;

        var overallResult = DetermineOverallResult(latencyChange, throughputChange, successRateChange);

        var insights = new List<string>();
        var warnings = new List<string>();

        if (latencyChange > 10)
            insights.Add($"Latency improved by {latencyChange:F1}%");
        else if (latencyChange < -10)
            warnings.Add($"Latency regressed by {Math.Abs(latencyChange):F1}%");

        if (throughputChange > 10)
            insights.Add($"Throughput improved by {throughputChange:F1}%");
        else if (throughputChange < -10)
            warnings.Add($"Throughput regressed by {Math.Abs(throughputChange):F1}%");

        var comparison = new BenchmarkComparison
        {
            Baseline = CreateSummary(baseline),
            Current = CreateSummary(current),
            LatencyChangePercent = latencyChange,
            ThroughputChangePercent = throughputChange,
            SuccessRateChangePercent = successRateChange,
            OverallResult = overallResult,
            Insights = insights,
            Warnings = warnings
        };

        return Task.FromResult(Result.Success(comparison));
    }

    public Task<Result<List<PerformanceRecommendation>>> GetRecommendationsAsync(
        Guid benchmarkId,
        CancellationToken cancellationToken = default)
    {
        if (!_benchmarkHistory.TryGetValue(benchmarkId, out var benchmark))
        {
            return Task.FromResult(Result.Failure<List<PerformanceRecommendation>>(
                PerformanceBenchmarkErrors.BenchmarkNotFound));
        }

        var recommendations = new List<PerformanceRecommendation>();

        // Generate recommendations based on metrics
        if (benchmark.AggregateMetrics.P95Latency > 500)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Title = "Optimize Query Performance",
                Description = "P95 latency exceeds 500ms. Consider adding indexes or optimizing queries.",
                Priority = RecommendationPriority.High,
                Category = RecommendationCategory.QueryOptimization,
                EstimatedImprovementPercent = 30,
                ImplementationGuide = "Review slow query logs and add appropriate indexes."
            });
        }

        if (benchmark.AggregateMetrics.SuccessRate < 99)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Title = "Improve Error Handling",
                Description = $"Success rate is {benchmark.AggregateMetrics.SuccessRate:F1}%. Investigate failures.",
                Priority = RecommendationPriority.Critical,
                Category = RecommendationCategory.Configuration,
                EstimatedImprovementPercent = 5
            });
        }

        // Check for cache opportunities
        var cacheHitScenario = benchmark.ScenarioResults.FirstOrDefault(s => s.Type == BenchmarkType.CacheHit);
        var cacheMissScenario = benchmark.ScenarioResults.FirstOrDefault(s => s.Type == BenchmarkType.CacheMiss);

        if (cacheHitScenario != null && cacheMissScenario != null)
        {
            var cacheEfficiency = cacheHitScenario.Metrics.MeanLatency / cacheMissScenario.Metrics.MeanLatency;
            if (cacheEfficiency > 0.3)
            {
                recommendations.Add(new PerformanceRecommendation
                {
                    Title = "Improve Cache Efficiency",
                    Description = "Cache hit latency is higher than expected. Consider cache warming or prefetching.",
                    Priority = RecommendationPriority.Medium,
                    Category = RecommendationCategory.Caching,
                    EstimatedImprovementPercent = 20
                });
            }
        }

        if (benchmark.ResourceMetrics?.PeakMemoryBytes > 1_000_000_000)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Title = "Optimize Memory Usage",
                Description = "Peak memory usage exceeds 1GB. Consider memory pooling or reducing object allocations.",
                Priority = RecommendationPriority.Medium,
                Category = RecommendationCategory.ResourceScaling
            });
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Title = "Performance is Good",
                Description = "No significant performance issues detected. Consider monitoring for regressions.",
                Priority = RecommendationPriority.Low,
                Category = RecommendationCategory.Configuration
            });
        }

        return Task.FromResult(Result.Success(recommendations));
    }

    #region Private Helper Methods

    private async Task<ScenarioResult> RunScenarioAsync(
        BenchmarkScenario scenario,
        int warmupIterations,
        int measurementIterations,
        int concurrentUsers,
        CancellationToken cancellationToken)
    {
        var latencies = new List<double>();
        var successful = 0;
        var failed = 0;

        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            await SimulateOperationAsync(scenario.Type, cancellationToken);
        }

        // Measurement
        if (concurrentUsers > 1)
        {
            var iterationsPerUser = measurementIterations / concurrentUsers;
            var tasks = Enumerable.Range(0, concurrentUsers)
                .Select(async _ =>
                {
                    var localLatencies = new List<double>();
                    var localSuccess = 0;
                    var localFailed = 0;

                    for (int i = 0; i < iterationsPerUser; i++)
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            await SimulateOperationAsync(scenario.Type, cancellationToken);
                            sw.Stop();
                            localLatencies.Add(sw.Elapsed.TotalMilliseconds);
                            localSuccess++;
                        }
                        catch
                        {
                            localFailed++;
                        }
                    }

                    return (localLatencies, localSuccess, localFailed);
                });

            var results = await Task.WhenAll(tasks);
            foreach (var (localLatencies, localSuccess, localFailed) in results)
            {
                latencies.AddRange(localLatencies);
                successful += localSuccess;
                failed += localFailed;
            }
        }
        else
        {
            for (int i = 0; i < measurementIterations; i++)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    await SimulateOperationAsync(scenario.Type, cancellationToken);
                    sw.Stop();
                    latencies.Add(sw.Elapsed.TotalMilliseconds);
                    successful++;
                }
                catch
                {
                    failed++;
                }
            }
        }

        return new ScenarioResult
        {
            ScenarioName = scenario.Name,
            Type = scenario.Type,
            TotalIterations = successful + failed,
            SuccessfulIterations = successful,
            FailedIterations = failed,
            Metrics = CalculateMetrics(latencies)
        };
    }

    private static async Task SimulateOperationAsync(BenchmarkType type, CancellationToken cancellationToken)
    {
        if (!_baseLatencies.TryGetValue(type, out var baseline))
        {
            baseline = (100, 30);
        }

        // Simulate latency with some variance
        var latency = baseline.Mean + Random.Shared.NextDouble() * baseline.StdDev * 2 - baseline.StdDev;
        latency = Math.Max(1, latency);

        await Task.Delay(TimeSpan.FromMilliseconds(latency / 10), cancellationToken); // Scale down for testing
    }

    private static PerformanceMetrics CalculateMetrics(List<double> latencies)
    {
        if (latencies.Count == 0)
        {
            return new PerformanceMetrics
            {
                MinLatency = 0,
                MaxLatency = 0,
                MeanLatency = 0,
                MedianLatency = 0,
                P90Latency = 0,
                P95Latency = 0,
                P99Latency = 0,
                StdDevLatency = 0,
                RequestsPerSecond = 0,
                OperationsPerSecond = 0,
                SuccessRate = 0,
                ErrorRate = 0
            };
        }

        var sorted = latencies.OrderBy(l => l).ToList();
        var mean = latencies.Average();
        var sumOfSquares = latencies.Sum(l => Math.Pow(l - mean, 2));
        var stdDev = Math.Sqrt(sumOfSquares / latencies.Count);

        return new PerformanceMetrics
        {
            MinLatency = sorted.First(),
            MaxLatency = sorted.Last(),
            MeanLatency = mean,
            MedianLatency = Percentile(sorted, 50),
            P90Latency = Percentile(sorted, 90),
            P95Latency = Percentile(sorted, 95),
            P99Latency = Percentile(sorted, 99),
            StdDevLatency = stdDev,
            RequestsPerSecond = 1000 / mean * latencies.Count, // Approximate
            OperationsPerSecond = 1000 / mean,
            SuccessRate = 100,
            ErrorRate = 0,
            LatencyHistogram = sorted.Count <= 100 ? sorted : null
        };
    }

    private static double Percentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }

    private static PerformanceMetrics CalculateAggregateMetrics(List<ScenarioResult> results)
    {
        var allLatencies = results
            .Where(r => r.Metrics.LatencyHistogram != null)
            .SelectMany(r => r.Metrics.LatencyHistogram!)
            .OrderBy(l => l)
            .ToList();

        if (allLatencies.Count == 0)
        {
            // Fall back to weighted average of scenario metrics
            var totalIterations = results.Sum(r => r.TotalIterations);
            if (totalIterations == 0)
            {
                return new PerformanceMetrics();
            }

            return new PerformanceMetrics
            {
                MinLatency = results.Min(r => r.Metrics.MinLatency),
                MaxLatency = results.Max(r => r.Metrics.MaxLatency),
                MeanLatency = results.Sum(r => r.Metrics.MeanLatency * r.TotalIterations) / totalIterations,
                MedianLatency = results.Average(r => r.Metrics.MedianLatency),
                P90Latency = results.Max(r => r.Metrics.P90Latency),
                P95Latency = results.Max(r => r.Metrics.P95Latency),
                P99Latency = results.Max(r => r.Metrics.P99Latency),
                StdDevLatency = results.Average(r => r.Metrics.StdDevLatency),
                RequestsPerSecond = results.Sum(r => r.Metrics.RequestsPerSecond),
                OperationsPerSecond = results.Sum(r => r.Metrics.OperationsPerSecond),
                SuccessRate = results.Sum(r => r.SuccessfulIterations) * 100.0 / totalIterations,
                ErrorRate = results.Sum(r => r.FailedIterations) * 100.0 / totalIterations
            };
        }

        return CalculateMetrics(allLatencies);
    }

    private static ResourceMetrics CollectResourceMetrics()
    {
        var process = Process.GetCurrentProcess();

        return new ResourceMetrics
        {
            PeakMemoryBytes = process.PeakWorkingSet64,
            AverageMemoryBytes = process.WorkingSet64,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            PeakCpuPercent = Random.Shared.NextDouble() * 30 + 10, // Simulated
            AverageCpuPercent = Random.Shared.NextDouble() * 20 + 5,
            PeakThreadCount = process.Threads.Count,
            PeakWorkerThreads = Environment.ProcessorCount * 2,
            PeakCompletionPortThreads = Environment.ProcessorCount
        };
    }

    private static int CalculateCurrentUsers(
        TimeSpan elapsed,
        TimeSpan rampUp,
        TimeSpan steady,
        TimeSpan rampDown,
        int startUsers,
        int maxUsers)
    {
        if (elapsed < rampUp)
        {
            var progress = elapsed.TotalSeconds / rampUp.TotalSeconds;
            return (int)(startUsers + (maxUsers - startUsers) * progress);
        }
        else if (elapsed < rampUp + steady)
        {
            return maxUsers;
        }
        else
        {
            var rampDownStart = rampUp + steady;
            var progress = (elapsed - rampDownStart).TotalSeconds / rampDown.TotalSeconds;
            return (int)(maxUsers - (maxUsers - startUsers) * progress);
        }
    }

    private static string GetPhase(TimeSpan elapsed, TimeSpan rampUp, TimeSpan steady)
    {
        if (elapsed < rampUp)
            return "Ramp Up";
        else if (elapsed < rampUp + steady)
            return "Steady State";
        else
            return "Ramp Down";
    }

    private static BenchmarkScenario SelectRandomScenario(List<BenchmarkScenario> scenarios)
    {
        var totalWeight = scenarios.Sum(s => s.Weight);
        var random = Random.Shared.Next(totalWeight);
        var cumulative = 0;

        foreach (var scenario in scenarios)
        {
            cumulative += scenario.Weight;
            if (random < cumulative)
                return scenario;
        }

        return scenarios.Last();
    }

    private static ComparisonResult DetermineOverallResult(
        double latencyChange,
        double throughputChange,
        double successRateChange)
    {
        var improvements = 0;
        var regressions = 0;

        if (latencyChange > 5) improvements++; else if (latencyChange < -5) regressions++;
        if (throughputChange > 5) improvements++; else if (throughputChange < -5) regressions++;
        if (successRateChange > 0.5) improvements++; else if (successRateChange < -0.5) regressions++;

        if (improvements > 0 && regressions == 0) return ComparisonResult.Improved;
        if (regressions > 0 && improvements == 0) return ComparisonResult.Regressed;
        if (improvements > 0 || regressions > 0) return ComparisonResult.Mixed;
        return ComparisonResult.NoChange;
    }

    private static BenchmarkSummary CreateSummary(BenchmarkResult result)
    {
        return new BenchmarkSummary
        {
            Id = result.Id,
            Name = result.Name,
            WorkspaceId = result.WorkspaceId,
            RunAt = result.StartedAt,
            Duration = result.Duration,
            Status = result.Status,
            MeanLatencyMs = result.AggregateMetrics.MeanLatency,
            P95LatencyMs = result.AggregateMetrics.P95Latency,
            RequestsPerSecond = result.AggregateMetrics.RequestsPerSecond,
            SuccessRate = result.AggregateMetrics.SuccessRate
        };
    }

    #endregion
}
