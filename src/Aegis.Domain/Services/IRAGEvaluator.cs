using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// RAG evaluation service for measuring quality metrics
/// Based on RAGAS (Retrieval Augmented Generation Assessment) methodology
/// </summary>
public interface IRAGEvaluator
{
    /// <summary>
    /// Evaluate a single RAG response comprehensively
    /// </summary>
    Task<Result<RAGEvaluationResult>> EvaluateAsync(
        RAGEvaluationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate faithfulness - is the answer grounded in the context?
    /// </summary>
    Task<Result<MetricScore>> EvaluateFaithfulnessAsync(
        string answer,
        List<string> contexts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate answer relevancy - is the answer relevant to the question?
    /// </summary>
    Task<Result<MetricScore>> EvaluateAnswerRelevancyAsync(
        string question,
        string answer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate context precision - are the contexts relevant and well-ranked?
    /// </summary>
    Task<Result<MetricScore>> EvaluateContextPrecisionAsync(
        string question,
        List<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate context recall - are all relevant contexts retrieved?
    /// </summary>
    Task<Result<MetricScore>> EvaluateContextRecallAsync(
        string question,
        List<string> contexts,
        string groundTruth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a batch evaluation on multiple samples
    /// </summary>
    Task<Result<BatchEvaluationResult>> EvaluateBatchAsync(
        List<RAGEvaluationRequest> requests,
        EvaluationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get evaluation statistics for a workspace
    /// </summary>
    Task<Result<EvaluationStatistics>> GetStatisticsAsync(
        Guid workspaceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run evaluation against a test dataset
    /// </summary>
    Task<Result<TestDatasetResult>> EvaluateTestDatasetAsync(
        Guid testDatasetId,
        EvaluationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a test dataset for evaluation
    /// </summary>
    Task<Result<Guid>> CreateTestDatasetAsync(
        TestDatasetRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available test datasets
    /// </summary>
    Task<Result<List<TestDatasetInfo>>> GetTestDatasetsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);
}

#region Request and Response Types

/// <summary>
/// Request for RAG evaluation
/// </summary>
public record RAGEvaluationRequest
{
    public Guid? Id { get; init; }
    public Guid? WorkspaceId { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required List<string> Contexts { get; init; }
    public string? GroundTruth { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Comprehensive RAG evaluation result
/// </summary>
public record RAGEvaluationResult
{
    public Guid Id { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }

    // Core RAGAS metrics (0-1 scale)
    public required MetricScore Faithfulness { get; init; }
    public required MetricScore AnswerRelevancy { get; init; }
    public required MetricScore ContextPrecision { get; init; }
    public MetricScore? ContextRecall { get; init; } // Requires ground truth

    // Composite scores
    public double OverallScore { get; init; }
    public QualityGrade Grade { get; init; }

    // Additional metrics
    public AnswerCorrectnessScore? AnswerCorrectness { get; init; }
    public HallucinationScore? HallucinationScore { get; init; }

    // Detailed feedback
    public List<EvaluationIssue> Issues { get; init; } = new();
    public List<string> Suggestions { get; init; } = new();

    // Metadata
    public DateTimeOffset EvaluatedAt { get; init; }
    public TimeSpan EvaluationDuration { get; init; }
}

/// <summary>
/// Individual metric score with explanation
/// </summary>
public record MetricScore
{
    public required string MetricName { get; init; }
    public double Score { get; init; }
    public string? Explanation { get; init; }
    public double Confidence { get; init; } = 1.0;
    public List<string>? SupportingEvidence { get; init; }
}

/// <summary>
/// Answer correctness evaluation (semantic + factual)
/// </summary>
public record AnswerCorrectnessScore
{
    public double SemanticSimilarity { get; init; }
    public double FactualOverlap { get; init; }
    public double CombinedScore { get; init; }
    public List<string>? MatchedFacts { get; init; }
    public List<string>? MissingFacts { get; init; }
}

/// <summary>
/// Hallucination detection score
/// </summary>
public record HallucinationScore
{
    public double Score { get; init; } // 0 = no hallucination, 1 = complete hallucination
    public List<HallucinatedClaim>? HallucinatedClaims { get; init; }
}

/// <summary>
/// Individual hallucinated claim
/// </summary>
public record HallucinatedClaim
{
    public required string Claim { get; init; }
    public double Confidence { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Evaluation issue found during analysis
/// </summary>
public record EvaluationIssue
{
    public required EvaluationIssueType Type { get; init; }
    public required IssueSeverity Severity { get; init; }
    public required string Description { get; init; }
    public string? Location { get; init; }
    public string? Suggestion { get; init; }
}

/// <summary>
/// Quality grade based on overall score
/// </summary>
public enum QualityGrade
{
    Excellent,  // 0.9+
    Good,       // 0.75-0.9
    Fair,       // 0.5-0.75
    Poor,       // 0.25-0.5
    VeryPoor    // 0-0.25
}

/// <summary>
/// Types of evaluation issues
/// </summary>
public enum EvaluationIssueType
{
    Hallucination,
    IncompleteAnswer,
    IrrelevantContent,
    MissingContext,
    FactualError,
    ContextMismatch,
    PoorCoherence,
    ExcessiveLength,
    InsufficientDetail
}

/// <summary>
/// Severity levels for issues
/// </summary>
public enum IssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}

#endregion

#region Batch Evaluation

/// <summary>
/// Options for batch evaluation
/// </summary>
public record EvaluationOptions
{
    public bool IncludeContextRecall { get; init; } = true;
    public bool IncludeAnswerCorrectness { get; init; } = true;
    public bool IncludeHallucinationCheck { get; init; } = true;
    public bool GenerateSuggestions { get; init; } = true;
    public int MaxConcurrency { get; init; } = 5;
    public bool StopOnFirstFailure { get; init; } = false;
}

/// <summary>
/// Result of batch evaluation
/// </summary>
public record BatchEvaluationResult
{
    public Guid BatchId { get; init; }
    public int TotalSamples { get; init; }
    public int SuccessfulEvaluations { get; init; }
    public int FailedEvaluations { get; init; }

    // Aggregate metrics
    public required AggregateMetrics AggregateMetrics { get; init; }

    // Distribution
    public Dictionary<QualityGrade, int> GradeDistribution { get; init; } = new();

    // Individual results
    public List<RAGEvaluationResult> Results { get; init; } = new();
    public List<EvaluationError> Errors { get; init; } = new();

    // Timing
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan TotalDuration => CompletedAt - StartedAt;
}

/// <summary>
/// Aggregate metrics across batch
/// </summary>
public record AggregateMetrics
{
    public double MeanFaithfulness { get; init; }
    public double MeanAnswerRelevancy { get; init; }
    public double MeanContextPrecision { get; init; }
    public double? MeanContextRecall { get; init; }
    public double MeanOverallScore { get; init; }

    // Standard deviations
    public double StdDevFaithfulness { get; init; }
    public double StdDevAnswerRelevancy { get; init; }
    public double StdDevContextPrecision { get; init; }
    public double? StdDevContextRecall { get; init; }

    // Min/Max
    public double MinOverallScore { get; init; }
    public double MaxOverallScore { get; init; }

    // Percentiles
    public double P50OverallScore { get; init; }
    public double P90OverallScore { get; init; }
    public double P99OverallScore { get; init; }
}

/// <summary>
/// Error during evaluation
/// </summary>
public record EvaluationError
{
    public Guid? RequestId { get; init; }
    public string? Question { get; init; }
    public required string ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

#endregion

#region Test Dataset

/// <summary>
/// Request to create a test dataset
/// </summary>
public record TestDatasetRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Guid? WorkspaceId { get; init; }
    public required List<TestSample> Samples { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Individual test sample
/// </summary>
public record TestSample
{
    public required string Question { get; init; }
    public required string GroundTruth { get; init; }
    public List<string>? ExpectedContexts { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Test dataset information
/// </summary>
public record TestDatasetInfo
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Guid? WorkspaceId { get; init; }
    public int SampleCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Result of evaluating against a test dataset
/// </summary>
public record TestDatasetResult
{
    public Guid TestDatasetId { get; init; }
    public required string DatasetName { get; init; }
    public required BatchEvaluationResult EvaluationResult { get; init; }

    // Comparison with previous runs
    public double? ScoreChange { get; init; }
    public TestDatasetResult? PreviousRun { get; init; }

    // Pass/fail based on thresholds
    public bool Passed { get; init; }
    public double PassThreshold { get; init; }
    public List<string>? FailureReasons { get; init; }
}

#endregion

#region Statistics

/// <summary>
/// Evaluation statistics for a workspace
/// </summary>
public record EvaluationStatistics
{
    public Guid? WorkspaceId { get; init; }
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }

    public int TotalEvaluations { get; init; }
    public required AggregateMetrics AggregateMetrics { get; init; }

    // Trends over time
    public List<MetricTrend> Trends { get; init; } = new();

    // Top issues
    public List<IssueFrequency> TopIssues { get; init; } = new();

    // Quality distribution
    public Dictionary<QualityGrade, int> GradeDistribution { get; init; } = new();
    public Dictionary<QualityGrade, double> GradePercentage { get; init; } = new();
}

/// <summary>
/// Metric trend over time
/// </summary>
public record MetricTrend
{
    public required string MetricName { get; init; }
    public List<TrendPoint> Points { get; init; } = new();
    public double TrendDirection { get; init; } // Positive = improving, Negative = declining
}

/// <summary>
/// Single point in a trend
/// </summary>
public record TrendPoint
{
    public DateTimeOffset Timestamp { get; init; }
    public double Value { get; init; }
    public int SampleCount { get; init; }
}

/// <summary>
/// Frequency of evaluation issues
/// </summary>
public record IssueFrequency
{
    public required EvaluationIssueType IssueType { get; init; }
    public int Count { get; init; }
    public double Percentage { get; init; }
}

#endregion

#region Error Definitions

public static class RAGEvaluatorErrors
{
    public static Error EvaluationFailed => Error.Internal(
        "RAGEvaluator.EvaluationFailed",
        "RAG evaluation failed");

    public static Error InvalidRequest => Error.Validation(
        "RAGEvaluator.InvalidRequest",
        "Invalid evaluation request");

    public static Error TestDatasetNotFound => Error.NotFound(
        "RAGEvaluator.TestDatasetNotFound",
        "Test dataset not found");

    public static Error NoSamples => Error.Validation(
        "RAGEvaluator.NoSamples",
        "No samples provided for evaluation");

    public static Error GroundTruthRequired => Error.Validation(
        "RAGEvaluator.GroundTruthRequired",
        "Ground truth is required for this metric");
}

#endregion
