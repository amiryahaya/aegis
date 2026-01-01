using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for executing operations with resilience policies (retry, circuit breaker, timeout)
/// </summary>
public interface IResilienceService
{
    /// <summary>
    /// Executes an async operation with retry and circuit breaker policies
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an async operation with custom resilience options
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> operation,
        ResilienceOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation that returns a Result
    /// </summary>
    Task<Result<T>> ExecuteWithResultAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the circuit breaker state for a specific operation
    /// </summary>
    CircuitBreakerState GetCircuitState(string operationKey);

    /// <summary>
    /// Manually resets the circuit breaker for a specific operation
    /// </summary>
    void ResetCircuit(string operationKey);
}

/// <summary>
/// Options for resilience policies
/// </summary>
public class ResilienceOptions
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries (exponential backoff applied)
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for the operation
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of failures before opening the circuit
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time window for counting failures
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Duration the circuit stays open before allowing a test request
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum throughput before circuit breaker activates
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Whether to use jitter for retry delays
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Creates default options optimized for LLM API calls
    /// </summary>
    public static ResilienceOptions ForLlmApi() => new()
    {
        MaxRetryAttempts = 3,
        RetryBaseDelay = TimeSpan.FromSeconds(2),
        MaxRetryDelay = TimeSpan.FromSeconds(60),
        Timeout = TimeSpan.FromMinutes(2),
        CircuitBreakerFailureThreshold = 3,
        CircuitBreakerBreakDuration = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Creates default options optimized for vector database calls
    /// </summary>
    public static ResilienceOptions ForVectorDb() => new()
    {
        MaxRetryAttempts = 3,
        RetryBaseDelay = TimeSpan.FromMilliseconds(500),
        MaxRetryDelay = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(30),
        CircuitBreakerFailureThreshold = 5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Creates default options optimized for database calls
    /// </summary>
    public static ResilienceOptions ForDatabase() => new()
    {
        MaxRetryAttempts = 3,
        RetryBaseDelay = TimeSpan.FromMilliseconds(200),
        MaxRetryDelay = TimeSpan.FromSeconds(5),
        Timeout = TimeSpan.FromSeconds(15),
        CircuitBreakerFailureThreshold = 5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15)
    };

    /// <summary>
    /// Creates default options optimized for external HTTP APIs
    /// </summary>
    public static ResilienceOptions ForExternalApi() => new()
    {
        MaxRetryAttempts = 2,
        RetryBaseDelay = TimeSpan.FromSeconds(1),
        MaxRetryDelay = TimeSpan.FromSeconds(15),
        Timeout = TimeSpan.FromSeconds(30),
        CircuitBreakerFailureThreshold = 5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30)
    };
}

/// <summary>
/// Circuit breaker state
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed, requests are allowed
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, requests are blocked
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, test request allowed
    /// </summary>
    HalfOpen,

    /// <summary>
    /// Circuit state is unknown or isolated
    /// </summary>
    Isolated
}

/// <summary>
/// Information about a circuit breaker
/// </summary>
public class CircuitBreakerInfo
{
    public required string OperationKey { get; init; }
    public CircuitBreakerState State { get; init; }
    public int FailureCount { get; init; }
    public DateTime? LastFailureTime { get; init; }
    public DateTime? CircuitOpenedTime { get; init; }
    public TimeSpan? TimeUntilReset { get; init; }
}
