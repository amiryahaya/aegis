using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Evaluation;

/// <summary>
/// In-memory implementation of RAG evaluator for development and testing
/// Uses heuristic-based scoring (production would use LLM-based evaluation)
/// </summary>
public class InMemoryRAGEvaluator : IRAGEvaluator
{
    private readonly ILogger<InMemoryRAGEvaluator> _logger;
    private readonly ConcurrentDictionary<Guid, TestDatasetInfo> _testDatasets = new();
    private readonly ConcurrentDictionary<Guid, List<TestSample>> _testSamples = new();
    private readonly ConcurrentDictionary<Guid, List<RAGEvaluationResult>> _evaluationHistory = new();

    public InMemoryRAGEvaluator(ILogger<InMemoryRAGEvaluator> logger)
    {
        _logger = logger;
    }

    public async Task<Result<RAGEvaluationResult>> EvaluateAsync(
        RAGEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Answer))
            {
                return Result.Failure<RAGEvaluationResult>(RAGEvaluatorErrors.InvalidRequest);
            }

            // Evaluate individual metrics
            var faithfulnessResult = await EvaluateFaithfulnessAsync(
                request.Answer, request.Contexts, cancellationToken);
            var relevancyResult = await EvaluateAnswerRelevancyAsync(
                request.Question, request.Answer, cancellationToken);
            var precisionResult = await EvaluateContextPrecisionAsync(
                request.Question, request.Contexts, request.GroundTruth, cancellationToken);

            if (faithfulnessResult.IsFailure || relevancyResult.IsFailure || precisionResult.IsFailure)
            {
                return Result.Failure<RAGEvaluationResult>(RAGEvaluatorErrors.EvaluationFailed);
            }

            MetricScore? contextRecall = null;
            if (!string.IsNullOrWhiteSpace(request.GroundTruth))
            {
                var recallResult = await EvaluateContextRecallAsync(
                    request.Question, request.Contexts, request.GroundTruth, cancellationToken);
                if (recallResult.IsSuccess)
                {
                    contextRecall = recallResult.Value;
                }
            }

            // Calculate overall score (weighted average)
            var overallScore = CalculateOverallScore(
                faithfulnessResult.Value.Score,
                relevancyResult.Value.Score,
                precisionResult.Value.Score,
                contextRecall?.Score);

            // Detect issues
            var issues = DetectIssues(
                request,
                faithfulnessResult.Value,
                relevancyResult.Value,
                precisionResult.Value);

            // Generate suggestions
            var suggestions = GenerateSuggestions(issues, overallScore);

            // Calculate hallucination score
            var hallucinationScore = CalculateHallucinationScore(
                request.Answer, request.Contexts);

            var result = new RAGEvaluationResult
            {
                Id = request.Id ?? UuidGenerator.NewId(),
                Question = request.Question,
                Answer = request.Answer,
                Faithfulness = faithfulnessResult.Value,
                AnswerRelevancy = relevancyResult.Value,
                ContextPrecision = precisionResult.Value,
                ContextRecall = contextRecall,
                OverallScore = overallScore,
                Grade = GetGrade(overallScore),
                HallucinationScore = hallucinationScore,
                Issues = issues,
                Suggestions = suggestions,
                EvaluatedAt = DateTimeOffset.UtcNow,
                EvaluationDuration = DateTime.UtcNow - startTime
            };

            // Store in history
            if (request.WorkspaceId.HasValue)
            {
                _evaluationHistory.AddOrUpdate(
                    request.WorkspaceId.Value,
                    _ => new List<RAGEvaluationResult> { result },
                    (_, list) => { list.Add(result); return list; });
            }

            _logger.LogInformation(
                "Evaluated RAG response for question '{Question}' - Score: {Score:F2}, Grade: {Grade}",
                request.Question.Length > 50 ? request.Question[..50] + "..." : request.Question,
                overallScore,
                result.Grade);

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating RAG response");
            return Result.Failure<RAGEvaluationResult>(RAGEvaluatorErrors.EvaluationFailed);
        }
    }

    public Task<Result<MetricScore>> EvaluateFaithfulnessAsync(
        string answer,
        List<string> contexts,
        CancellationToken cancellationToken = default)
    {
        if (contexts == null || contexts.Count == 0)
        {
            return Task.FromResult(Result.Success(new MetricScore
            {
                MetricName = "Faithfulness",
                Score = 0.0,
                Explanation = "No context provided",
                Confidence = 1.0
            }));
        }

        // Heuristic: Check how many words from the answer appear in contexts
        var answerWords = ExtractWords(answer);
        var contextWords = contexts.SelectMany(ExtractWords).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matchingWords = answerWords.Count(w => contextWords.Contains(w));
        var score = answerWords.Count > 0 ? (double)matchingWords / answerWords.Count : 0.0;

        // Boost score if answer is shorter (more concise)
        if (answer.Length < 500)
            score = Math.Min(1.0, score * 1.1);

        // Clamp score
        score = Math.Clamp(score, 0.0, 1.0);

        var result = new MetricScore
        {
            MetricName = "Faithfulness",
            Score = score,
            Explanation = score > 0.7
                ? "Answer is well-grounded in the provided context"
                : score > 0.4
                    ? "Answer is partially grounded in context"
                    : "Answer may contain information not present in context",
            Confidence = 0.8,
            SupportingEvidence = contexts.Take(2).ToList()
        };

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<MetricScore>> EvaluateAnswerRelevancyAsync(
        string question,
        string answer,
        CancellationToken cancellationToken = default)
    {
        // Heuristic: Check keyword overlap between question and answer
        var questionWords = ExtractWords(question);
        var answerWords = ExtractWords(answer);

        var matchingWords = questionWords.Count(w => answerWords.Contains(w, StringComparer.OrdinalIgnoreCase));
        var baseScore = questionWords.Count > 0 ? (double)matchingWords / questionWords.Count : 0.0;

        // Adjust for answer length (too short or too long is less relevant)
        var lengthPenalty = 1.0;
        if (answer.Length < 50)
            lengthPenalty = 0.8;
        else if (answer.Length > 2000)
            lengthPenalty = 0.9;

        var score = Math.Clamp(baseScore * lengthPenalty + 0.3, 0.0, 1.0); // Base boost for non-empty answers

        var result = new MetricScore
        {
            MetricName = "AnswerRelevancy",
            Score = score,
            Explanation = score > 0.7
                ? "Answer directly addresses the question"
                : score > 0.4
                    ? "Answer partially addresses the question"
                    : "Answer may not fully address the question",
            Confidence = 0.75
        };

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<MetricScore>> EvaluateContextPrecisionAsync(
        string question,
        List<string> contexts,
        string? groundTruth = null,
        CancellationToken cancellationToken = default)
    {
        if (contexts == null || contexts.Count == 0)
        {
            return Task.FromResult(Result.Success(new MetricScore
            {
                MetricName = "ContextPrecision",
                Score = 0.0,
                Explanation = "No contexts provided",
                Confidence = 1.0
            }));
        }

        // Heuristic: Check relevance of each context to the question
        var questionWords = ExtractWords(question).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scores = contexts.Select(context =>
        {
            var contextWords = ExtractWords(context);
            var matching = contextWords.Count(w => questionWords.Contains(w));
            return contextWords.Count > 0 ? (double)matching / Math.Min(questionWords.Count, contextWords.Count / 2) : 0.0;
        }).ToList();

        // Higher weight for earlier contexts (ranking matters)
        var weightedScore = 0.0;
        var totalWeight = 0.0;
        for (int i = 0; i < scores.Count; i++)
        {
            var weight = 1.0 / (i + 1);
            weightedScore += scores[i] * weight;
            totalWeight += weight;
        }

        var score = totalWeight > 0 ? Math.Clamp(weightedScore / totalWeight, 0.0, 1.0) : 0.0;

        var result = new MetricScore
        {
            MetricName = "ContextPrecision",
            Score = score,
            Explanation = score > 0.7
                ? "Retrieved contexts are highly relevant and well-ranked"
                : score > 0.4
                    ? "Retrieved contexts are moderately relevant"
                    : "Retrieved contexts may lack relevance to the question",
            Confidence = 0.7
        };

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<MetricScore>> EvaluateContextRecallAsync(
        string question,
        List<string> contexts,
        string groundTruth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groundTruth))
        {
            return Task.FromResult(Result.Failure<MetricScore>(RAGEvaluatorErrors.GroundTruthRequired));
        }

        // Heuristic: Check how much of ground truth is covered by contexts
        var groundTruthWords = ExtractWords(groundTruth).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contextWords = contexts.SelectMany(ExtractWords).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coveredWords = groundTruthWords.Count(w => contextWords.Contains(w));
        var score = groundTruthWords.Count > 0 ? (double)coveredWords / groundTruthWords.Count : 0.0;
        score = Math.Clamp(score, 0.0, 1.0);

        var result = new MetricScore
        {
            MetricName = "ContextRecall",
            Score = score,
            Explanation = score > 0.7
                ? "Contexts cover most of the expected answer"
                : score > 0.4
                    ? "Contexts partially cover the expected answer"
                    : "Contexts may be missing important information",
            Confidence = 0.75
        };

        return Task.FromResult(Result.Success(result));
    }

    public async Task<Result<BatchEvaluationResult>> EvaluateBatchAsync(
        List<RAGEvaluationRequest> requests,
        EvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new EvaluationOptions();
        var startTime = DateTimeOffset.UtcNow;

        if (requests == null || requests.Count == 0)
        {
            return Result.Failure<BatchEvaluationResult>(RAGEvaluatorErrors.NoSamples);
        }

        var results = new List<RAGEvaluationResult>();
        var errors = new List<EvaluationError>();

        foreach (var request in requests)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var result = await EvaluateAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                results.Add(result.Value);
            }
            else
            {
                errors.Add(new EvaluationError
                {
                    RequestId = request.Id,
                    Question = request.Question,
                    ErrorMessage = result.Error?.Message ?? "Unknown error",
                    OccurredAt = DateTimeOffset.UtcNow
                });

                if (options.StopOnFirstFailure)
                    break;
            }
        }

        var aggregateMetrics = CalculateAggregateMetrics(results);
        var gradeDistribution = results
            .GroupBy(r => r.Grade)
            .ToDictionary(g => g.Key, g => g.Count());

        var batchResult = new BatchEvaluationResult
        {
            BatchId = UuidGenerator.NewId(),
            TotalSamples = requests.Count,
            SuccessfulEvaluations = results.Count,
            FailedEvaluations = errors.Count,
            AggregateMetrics = aggregateMetrics,
            GradeDistribution = gradeDistribution,
            Results = results,
            Errors = errors,
            StartedAt = startTime,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Batch evaluation completed: {Success}/{Total} successful, Mean score: {Score:F2}",
            results.Count, requests.Count, aggregateMetrics.MeanOverallScore);

        return Result.Success(batchResult);
    }

    public Task<Result<EvaluationStatistics>> GetStatisticsAsync(
        Guid workspaceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        from ??= DateTimeOffset.UtcNow.AddDays(-30);
        to ??= DateTimeOffset.UtcNow;

        if (!_evaluationHistory.TryGetValue(workspaceId, out var history))
        {
            history = new List<RAGEvaluationResult>();
        }

        var filteredResults = history
            .Where(r => r.EvaluatedAt >= from && r.EvaluatedAt <= to)
            .ToList();

        var aggregateMetrics = CalculateAggregateMetrics(filteredResults);

        var gradeDistribution = filteredResults
            .GroupBy(r => r.Grade)
            .ToDictionary(g => g.Key, g => g.Count());

        var gradePercentage = filteredResults.Count > 0
            ? gradeDistribution.ToDictionary(
                g => g.Key,
                g => (double)g.Value / filteredResults.Count * 100)
            : new Dictionary<QualityGrade, double>();

        // Calculate issue frequencies
        var allIssues = filteredResults.SelectMany(r => r.Issues).ToList();
        var issueFrequencies = allIssues
            .GroupBy(i => i.Type)
            .Select(g => new IssueFrequency
            {
                IssueType = g.Key,
                Count = g.Count(),
                Percentage = allIssues.Count > 0 ? (double)g.Count() / allIssues.Count * 100 : 0
            })
            .OrderByDescending(i => i.Count)
            .Take(5)
            .ToList();

        // Generate trends (simplified - group by day)
        var trends = new List<MetricTrend>
        {
            GenerateTrend("OverallScore", filteredResults, r => r.OverallScore),
            GenerateTrend("Faithfulness", filteredResults, r => r.Faithfulness.Score),
            GenerateTrend("AnswerRelevancy", filteredResults, r => r.AnswerRelevancy.Score)
        };

        var stats = new EvaluationStatistics
        {
            WorkspaceId = workspaceId,
            From = from.Value,
            To = to.Value,
            TotalEvaluations = filteredResults.Count,
            AggregateMetrics = aggregateMetrics,
            Trends = trends,
            TopIssues = issueFrequencies,
            GradeDistribution = gradeDistribution,
            GradePercentage = gradePercentage
        };

        return Task.FromResult(Result.Success(stats));
    }

    public async Task<Result<TestDatasetResult>> EvaluateTestDatasetAsync(
        Guid testDatasetId,
        EvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!_testDatasets.TryGetValue(testDatasetId, out var dataset) ||
            !_testSamples.TryGetValue(testDatasetId, out var samples))
        {
            return Result.Failure<TestDatasetResult>(RAGEvaluatorErrors.TestDatasetNotFound);
        }

        // Convert samples to evaluation requests (simulate retrieval for testing)
        var requests = samples.Select(s => new RAGEvaluationRequest
        {
            Question = s.Question,
            Answer = s.GroundTruth, // In testing, we use ground truth as the simulated answer
            Contexts = s.ExpectedContexts ?? new List<string> { s.GroundTruth },
            GroundTruth = s.GroundTruth
        }).ToList();

        var batchResult = await EvaluateBatchAsync(requests, options, cancellationToken);
        if (batchResult.IsFailure)
        {
            return Result.Failure<TestDatasetResult>(batchResult.Error!);
        }

        // Update last used
        _testDatasets[testDatasetId] = dataset with { LastUsedAt = DateTimeOffset.UtcNow };

        var passThreshold = 0.7;
        var passed = batchResult.Value.AggregateMetrics.MeanOverallScore >= passThreshold;

        var result = new TestDatasetResult
        {
            TestDatasetId = testDatasetId,
            DatasetName = dataset.Name,
            EvaluationResult = batchResult.Value,
            Passed = passed,
            PassThreshold = passThreshold,
            FailureReasons = passed ? null : new List<string>
            {
                $"Mean overall score ({batchResult.Value.AggregateMetrics.MeanOverallScore:F2}) " +
                $"is below threshold ({passThreshold:F2})"
            }
        };

        return Result.Success(result);
    }

    public Task<Result<Guid>> CreateTestDatasetAsync(
        TestDatasetRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Samples == null || request.Samples.Count == 0)
        {
            return Task.FromResult(Result.Failure<Guid>(RAGEvaluatorErrors.InvalidRequest));
        }

        var id = UuidGenerator.NewId();

        var datasetInfo = new TestDatasetInfo
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            WorkspaceId = request.WorkspaceId,
            SampleCount = request.Samples.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            Tags = request.Tags
        };

        _testDatasets[id] = datasetInfo;
        _testSamples[id] = request.Samples.ToList();

        _logger.LogInformation(
            "Created test dataset '{Name}' with {SampleCount} samples",
            request.Name, request.Samples.Count);

        return Task.FromResult(Result.Success(id));
    }

    public Task<Result<List<TestDatasetInfo>>> GetTestDatasetsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var datasets = _testDatasets.Values.AsEnumerable();

        if (workspaceId.HasValue)
        {
            datasets = datasets.Where(d => d.WorkspaceId == workspaceId);
        }

        var result = datasets
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        return Task.FromResult(Result.Success(result));
    }

    #region Private Helper Methods

    private static List<string> ExtractWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        return text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}' },
            StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Filter out very short words
            .Select(w => w.ToLowerInvariant())
            .ToList();
    }

    private static double CalculateOverallScore(
        double faithfulness,
        double answerRelevancy,
        double contextPrecision,
        double? contextRecall)
    {
        // Weighted average with faithfulness having higher weight
        var weights = new Dictionary<string, double>
        {
            ["faithfulness"] = 0.35,
            ["answerRelevancy"] = 0.30,
            ["contextPrecision"] = 0.20,
            ["contextRecall"] = 0.15
        };

        var weightedSum = faithfulness * weights["faithfulness"] +
                          answerRelevancy * weights["answerRelevancy"] +
                          contextPrecision * weights["contextPrecision"];
        var totalWeight = weights["faithfulness"] + weights["answerRelevancy"] + weights["contextPrecision"];

        if (contextRecall.HasValue)
        {
            weightedSum += contextRecall.Value * weights["contextRecall"];
            totalWeight += weights["contextRecall"];
        }

        return weightedSum / totalWeight;
    }

    private static QualityGrade GetGrade(double score)
    {
        return score switch
        {
            >= 0.9 => QualityGrade.Excellent,
            >= 0.75 => QualityGrade.Good,
            >= 0.5 => QualityGrade.Fair,
            >= 0.25 => QualityGrade.Poor,
            _ => QualityGrade.VeryPoor
        };
    }

    private static List<EvaluationIssue> DetectIssues(
        RAGEvaluationRequest request,
        MetricScore faithfulness,
        MetricScore answerRelevancy,
        MetricScore contextPrecision)
    {
        var issues = new List<EvaluationIssue>();

        if (faithfulness.Score < 0.5)
        {
            issues.Add(new EvaluationIssue
            {
                Type = EvaluationIssueType.Hallucination,
                Severity = faithfulness.Score < 0.3 ? IssueSeverity.Critical : IssueSeverity.High,
                Description = "Answer contains information not grounded in the provided context",
                Suggestion = "Ensure the answer only contains facts from the retrieved documents"
            });
        }

        if (answerRelevancy.Score < 0.5)
        {
            issues.Add(new EvaluationIssue
            {
                Type = EvaluationIssueType.IrrelevantContent,
                Severity = IssueSeverity.Medium,
                Description = "Answer may not fully address the user's question",
                Suggestion = "Focus on directly answering what the user asked"
            });
        }

        if (contextPrecision.Score < 0.4)
        {
            issues.Add(new EvaluationIssue
            {
                Type = EvaluationIssueType.MissingContext,
                Severity = IssueSeverity.Medium,
                Description = "Retrieved contexts may not be relevant to the question",
                Suggestion = "Improve retrieval query or context ranking"
            });
        }

        if (request.Answer.Length < 50)
        {
            issues.Add(new EvaluationIssue
            {
                Type = EvaluationIssueType.InsufficientDetail,
                Severity = IssueSeverity.Low,
                Description = "Answer may lack sufficient detail",
                Suggestion = "Consider providing more comprehensive information"
            });
        }

        if (request.Answer.Length > 2000)
        {
            issues.Add(new EvaluationIssue
            {
                Type = EvaluationIssueType.ExcessiveLength,
                Severity = IssueSeverity.Low,
                Description = "Answer may be overly verbose",
                Suggestion = "Consider providing a more concise response"
            });
        }

        return issues;
    }

    private static List<string> GenerateSuggestions(List<EvaluationIssue> issues, double overallScore)
    {
        var suggestions = new List<string>();

        if (overallScore < 0.5)
        {
            suggestions.Add("Consider reviewing the retrieval strategy to improve context quality");
        }

        if (issues.Any(i => i.Type == EvaluationIssueType.Hallucination))
        {
            suggestions.Add("Implement stricter grounding checks to prevent hallucination");
        }

        if (issues.Any(i => i.Type == EvaluationIssueType.MissingContext))
        {
            suggestions.Add("Improve document chunking or increase the number of retrieved contexts");
        }

        if (issues.Count == 0 && overallScore >= 0.8)
        {
            suggestions.Add("Response quality is good - maintain current configuration");
        }

        return suggestions;
    }

    private static HallucinationScore CalculateHallucinationScore(string answer, List<string> contexts)
    {
        // Simple heuristic: look for claims not grounded in context
        var answerSentences = answer.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Trim().Length > 10)
            .ToList();

        var contextText = string.Join(" ", contexts).ToLowerInvariant();
        var hallucinatedClaims = new List<HallucinatedClaim>();

        foreach (var sentence in answerSentences)
        {
            var sentenceWords = ExtractWords(sentence);
            var matchingWords = sentenceWords.Count(w => contextText.Contains(w, StringComparison.OrdinalIgnoreCase));
            var groundedness = sentenceWords.Count > 0 ? (double)matchingWords / sentenceWords.Count : 0;

            if (groundedness < 0.3 && sentenceWords.Count > 3)
            {
                hallucinatedClaims.Add(new HallucinatedClaim
                {
                    Claim = sentence.Trim(),
                    Confidence = 1.0 - groundedness,
                    Reason = "Claim not well-supported by retrieved contexts"
                });
            }
        }

        var score = answerSentences.Count > 0
            ? (double)hallucinatedClaims.Count / answerSentences.Count
            : 0.0;

        return new HallucinationScore
        {
            Score = score,
            HallucinatedClaims = hallucinatedClaims
        };
    }

    private static AggregateMetrics CalculateAggregateMetrics(List<RAGEvaluationResult> results)
    {
        if (results.Count == 0)
        {
            return new AggregateMetrics
            {
                MeanFaithfulness = 0,
                MeanAnswerRelevancy = 0,
                MeanContextPrecision = 0,
                MeanOverallScore = 0,
                StdDevFaithfulness = 0,
                StdDevAnswerRelevancy = 0,
                StdDevContextPrecision = 0,
                MinOverallScore = 0,
                MaxOverallScore = 0,
                P50OverallScore = 0,
                P90OverallScore = 0,
                P99OverallScore = 0
            };
        }

        var faithfulnessScores = results.Select(r => r.Faithfulness.Score).ToList();
        var relevancyScores = results.Select(r => r.AnswerRelevancy.Score).ToList();
        var precisionScores = results.Select(r => r.ContextPrecision.Score).ToList();
        var overallScores = results.Select(r => r.OverallScore).OrderBy(s => s).ToList();
        var recallScores = results.Where(r => r.ContextRecall != null)
            .Select(r => r.ContextRecall!.Score).ToList();

        return new AggregateMetrics
        {
            MeanFaithfulness = faithfulnessScores.Average(),
            MeanAnswerRelevancy = relevancyScores.Average(),
            MeanContextPrecision = precisionScores.Average(),
            MeanContextRecall = recallScores.Count > 0 ? recallScores.Average() : null,
            MeanOverallScore = overallScores.Average(),
            StdDevFaithfulness = CalculateStdDev(faithfulnessScores),
            StdDevAnswerRelevancy = CalculateStdDev(relevancyScores),
            StdDevContextPrecision = CalculateStdDev(precisionScores),
            StdDevContextRecall = recallScores.Count > 0 ? CalculateStdDev(recallScores) : null,
            MinOverallScore = overallScores.Min(),
            MaxOverallScore = overallScores.Max(),
            P50OverallScore = Percentile(overallScores, 50),
            P90OverallScore = Percentile(overallScores, 90),
            P99OverallScore = Percentile(overallScores, 99)
        };
    }

    private static double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private static double Percentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }

    private static MetricTrend GenerateTrend(
        string metricName,
        List<RAGEvaluationResult> results,
        Func<RAGEvaluationResult, double> selector)
    {
        var points = results
            .GroupBy(r => r.EvaluatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint
            {
                Timestamp = g.Key,
                Value = g.Average(selector),
                SampleCount = g.Count()
            })
            .ToList();

        // Calculate trend direction (simple linear regression slope)
        var trendDirection = 0.0;
        if (points.Count >= 2)
        {
            var n = points.Count;
            var sumX = Enumerable.Range(0, n).Sum();
            var sumY = points.Sum(p => p.Value);
            var sumXY = points.Select((p, i) => i * p.Value).Sum();
            var sumX2 = Enumerable.Range(0, n).Sum(i => i * i);

            trendDirection = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        }

        return new MetricTrend
        {
            MetricName = metricName,
            Points = points,
            TrendDirection = trendDirection
        };
    }

    #endregion
}
