using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Aegis.Infrastructure.Services.Resilience;

/// <summary>
/// Resilience service implementation using Polly v8
/// </summary>
public class PollyResilienceService : IResilienceService
{
    private readonly ILogger<PollyResilienceService> _logger;
    private readonly ConcurrentDictionary<string, object> _pipelines = new();
    private readonly ConcurrentDictionary<string, CircuitBreakerStateInfo> _circuitStates = new();
    private readonly ResilienceOptions _defaultOptions;

    public PollyResilienceService(ILogger<PollyResilienceService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultOptions = new ResilienceOptions();
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(operationKey, operation, _defaultOptions, cancellationToken);
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        ResilienceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationKey);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);

        var pipeline = GetOrCreatePipeline<T>(operationKey, options);

        try
        {
            var result = await pipeline.ExecuteAsync(
                async ct => await operation(ct),
                cancellationToken);

            return Result<T>.Success(result);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex,
                "Circuit breaker is open for operation {OperationKey}",
                operationKey);

            return Result<T>.Failure(Error.ServiceUnavailable(
                "CircuitBreaker.Open",
                $"Service temporarily unavailable. Please try again later. Operation: {operationKey}"));
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex,
                "Operation {OperationKey} timed out after {Timeout}",
                operationKey, options.Timeout);

            return Result<T>.Failure(Error.Timeout(
                "Operation.Timeout",
                $"Operation timed out after {options.Timeout.TotalSeconds} seconds"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Operation {OperationKey} was cancelled",
                operationKey);

            return Result<T>.Failure(Error.Internal(
                "Operation.Cancelled",
                "Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Operation {OperationKey} failed after all retry attempts",
                operationKey);

            return Result<T>.Failure(Error.Internal(
                "Operation.Failed",
                $"Operation failed: {ex.Message}"));
        }
    }

    public async Task<Result<T>> ExecuteWithResultAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            operationKey,
            async ct =>
            {
                var innerResult = await operation(ct);
                if (innerResult.IsFailure)
                {
                    throw new ResultFailureException(innerResult.Error!);
                }
                return innerResult.Value!;
            },
            cancellationToken);

        return result;
    }

    public CircuitBreakerState GetCircuitState(string operationKey)
    {
        if (_circuitStates.TryGetValue(operationKey, out var stateInfo))
        {
            return stateInfo.State;
        }

        return CircuitBreakerState.Closed;
    }

    public void ResetCircuit(string operationKey)
    {
        // Remove all pipelines for this operation key (all type variants)
        var keysToRemove = _pipelines.Keys.Where(k => k.StartsWith($"{operationKey}:")).ToList();
        foreach (var key in keysToRemove)
        {
            _pipelines.TryRemove(key, out _);
        }
        _circuitStates.TryRemove(operationKey, out _);

        _logger.LogInformation(
            "Circuit breaker reset for operation {OperationKey}",
            operationKey);
    }

    private ResiliencePipeline<T> GetOrCreatePipeline<T>(string operationKey, ResilienceOptions options)
    {
        var key = $"{operationKey}:{typeof(T).Name}";

        var pipeline = _pipelines.GetOrAdd(key, _ => (object)CreatePipeline<T>(operationKey, options));

        return (ResiliencePipeline<T>)pipeline;
    }

    private ResiliencePipeline<T> CreatePipeline<T>(string operationKey, ResilienceOptions options)
    {
        var pipelineBuilder = new ResiliencePipelineBuilder<T>();

        // Add timeout
        pipelineBuilder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = options.Timeout,
            OnTimeout = args =>
            {
                _logger.LogWarning(
                    "Timeout triggered for operation {OperationKey} after {Timeout}",
                    operationKey, args.Timeout);
                return ValueTask.CompletedTask;
            }
        });

        // Add retry with exponential backoff
        pipelineBuilder.AddRetry(new RetryStrategyOptions<T>
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            Delay = options.RetryBaseDelay,
            MaxDelay = options.MaxRetryDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = options.UseJitter,
            ShouldHandle = new PredicateBuilder<T>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                .Handle<IOException>(),
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Retry {AttemptNumber} for operation {OperationKey} after {Delay}. Exception: {ExceptionMessage}",
                    args.AttemptNumber,
                    operationKey,
                    args.RetryDelay,
                    args.Outcome.Exception?.Message ?? "No exception");
                return ValueTask.CompletedTask;
            }
        });

        // Add circuit breaker
        pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
        {
            FailureRatio = 0.5,
            SamplingDuration = options.CircuitBreakerSamplingDuration,
            MinimumThroughput = options.CircuitBreakerMinimumThroughput,
            BreakDuration = options.CircuitBreakerBreakDuration,
            ShouldHandle = new PredicateBuilder<T>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutException>()
                .Handle<TimeoutRejectedException>()
                .Handle<IOException>(),
            OnOpened = args =>
            {
                _circuitStates[operationKey] = new CircuitBreakerStateInfo
                {
                    State = CircuitBreakerState.Open,
                    OpenedAt = DateTime.UtcNow,
                    BreakDuration = args.BreakDuration
                };

                _logger.LogWarning(
                    "Circuit breaker OPENED for operation {OperationKey}. Will remain open for {BreakDuration}",
                    operationKey, args.BreakDuration);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                _circuitStates[operationKey] = new CircuitBreakerStateInfo
                {
                    State = CircuitBreakerState.Closed
                };

                _logger.LogInformation(
                    "Circuit breaker CLOSED for operation {OperationKey}",
                    operationKey);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                _circuitStates[operationKey] = new CircuitBreakerStateInfo
                {
                    State = CircuitBreakerState.HalfOpen
                };

                _logger.LogInformation(
                    "Circuit breaker HALF-OPEN for operation {OperationKey}. Testing with next request",
                    operationKey);
                return ValueTask.CompletedTask;
            }
        });

        return pipelineBuilder.Build();
    }

    private class CircuitBreakerStateInfo
    {
        public CircuitBreakerState State { get; init; }
        public DateTime? OpenedAt { get; init; }
        public TimeSpan BreakDuration { get; init; }
    }
}

/// <summary>
/// Exception wrapper for Result failures to enable retry handling
/// </summary>
internal class ResultFailureException : Exception
{
    public Error Error { get; }

    public ResultFailureException(Error error)
        : base(error.Message)
    {
        Error = error;
    }
}
