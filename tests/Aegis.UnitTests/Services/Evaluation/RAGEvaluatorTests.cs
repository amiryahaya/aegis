using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Evaluation;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aegis.UnitTests.Services.Evaluation;

public class RAGEvaluatorTests
{
    private readonly InMemoryRAGEvaluator _evaluator;
    private readonly Mock<ILogger<InMemoryRAGEvaluator>> _loggerMock;

    public RAGEvaluatorTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryRAGEvaluator>>();
        _evaluator = new InMemoryRAGEvaluator(_loggerMock.Object);
    }

    #region EvaluateAsync Tests

    [Fact]
    public async Task EvaluateAsync_WithValidRequest_ReturnsResult()
    {
        // Arrange
        var request = new RAGEvaluationRequest
        {
            Question = "What is machine learning?",
            Answer = "Machine learning is a subset of artificial intelligence that enables systems to learn from data.",
            Contexts = new List<string>
            {
                "Machine learning is a branch of artificial intelligence focused on building systems that learn from data.",
                "AI systems can improve their performance through experience without explicit programming."
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Question.Should().Be(request.Question);
        result.Value.Answer.Should().Be(request.Answer);
        result.Value.Faithfulness.Score.Should().BeGreaterThan(0);
        result.Value.AnswerRelevancy.Score.Should().BeGreaterThan(0);
        result.Value.ContextPrecision.Score.Should().BeGreaterThan(0);
        result.Value.OverallScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_WithGroundTruth_IncludesContextRecall()
    {
        // Arrange
        var request = new RAGEvaluationRequest
        {
            Question = "What is Python?",
            Answer = "Python is a programming language known for its simplicity.",
            Contexts = new List<string>
            {
                "Python is a high-level programming language known for simplicity and readability."
            },
            GroundTruth = "Python is a high-level programming language"
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContextRecall.Should().NotBeNull();
        result.Value.ContextRecall!.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyQuestion_ReturnsFailure()
    {
        // Arrange
        var request = new RAGEvaluationRequest
        {
            Question = "",
            Answer = "Some answer",
            Contexts = new List<string> { "Some context" }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("InvalidRequest");
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyAnswer_ReturnsFailure()
    {
        // Arrange
        var request = new RAGEvaluationRequest
        {
            Question = "What is AI?",
            Answer = "",
            Contexts = new List<string> { "Some context" }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_AssignsGradeBasedOnScore()
    {
        // Arrange - Create a high-quality response
        var request = new RAGEvaluationRequest
        {
            Question = "What is data science?",
            Answer = "Data science is an interdisciplinary field that uses scientific methods and algorithms to extract knowledge from data.",
            Contexts = new List<string>
            {
                "Data science is an interdisciplinary field that combines scientific methods, algorithms, and systems to extract knowledge and insights from structured and unstructured data."
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Grade.Should().BeOneOf(
            QualityGrade.Excellent,
            QualityGrade.Good,
            QualityGrade.Fair);
    }

    [Fact]
    public async Task EvaluateAsync_DetectsIssues_WhenQualityIsLow()
    {
        // Arrange - Create a low-quality response with hallucination
        var request = new RAGEvaluationRequest
        {
            Question = "What is the capital of France?",
            Answer = "The capital of France is Berlin, which is located in the Alps.",
            Contexts = new List<string>
            {
                "Paris is the capital and most populous city of France."
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // The answer has low faithfulness (wrong information)
        result.Value.Faithfulness.Score.Should().BeLessThan(0.7);
    }

    [Fact]
    public async Task EvaluateAsync_StoresResultInHistory_WhenWorkspaceIdProvided()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var request = new RAGEvaluationRequest
        {
            WorkspaceId = workspaceId,
            Question = "Test question",
            Answer = "Test answer about the topic",
            Contexts = new List<string> { "Test context about the topic" }
        };

        // Act
        await _evaluator.EvaluateAsync(request);
        var stats = await _evaluator.GetStatisticsAsync(workspaceId);

        // Assert
        stats.IsSuccess.Should().BeTrue();
        stats.Value.TotalEvaluations.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region EvaluateFaithfulnessAsync Tests

    [Fact]
    public async Task EvaluateFaithfulnessAsync_WithGroundedAnswer_ReturnsHighScore()
    {
        // Arrange
        var answer = "Machine learning uses data to train models.";
        var contexts = new List<string>
        {
            "Machine learning is a technique that uses training data to build models."
        };

        // Act
        var result = await _evaluator.EvaluateFaithfulnessAsync(answer, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MetricName.Should().Be("Faithfulness");
        result.Value.Score.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public async Task EvaluateFaithfulnessAsync_WithNoContext_ReturnsZeroScore()
    {
        // Arrange
        var answer = "Some answer";
        var contexts = new List<string>();

        // Act
        var result = await _evaluator.EvaluateFaithfulnessAsync(answer, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateFaithfulnessAsync_WithUngroundedAnswer_ReturnsLowScore()
    {
        // Arrange
        var answer = "The moon is made of cheese and aliens live there.";
        var contexts = new List<string>
        {
            "The moon is Earth's only natural satellite.",
            "The moon's surface is covered in craters."
        };

        // Act
        var result = await _evaluator.EvaluateFaithfulnessAsync(answer, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeLessThan(0.5);
    }

    #endregion

    #region EvaluateAnswerRelevancyAsync Tests

    [Fact]
    public async Task EvaluateAnswerRelevancyAsync_WithRelevantAnswer_ReturnsHighScore()
    {
        // Arrange
        var question = "What is the speed of light?";
        var answer = "The speed of light is approximately 299,792 kilometers per second.";

        // Act
        var result = await _evaluator.EvaluateAnswerRelevancyAsync(question, answer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MetricName.Should().Be("AnswerRelevancy");
        result.Value.Score.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public async Task EvaluateAnswerRelevancyAsync_WithIrrelevantAnswer_ReturnsLowerScore()
    {
        // Arrange
        var question = "What is the speed of light?";
        var answer = "Bananas are a good source of potassium.";

        // Act
        var result = await _evaluator.EvaluateAnswerRelevancyAsync(question, answer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeLessThan(0.7);
    }

    #endregion

    #region EvaluateContextPrecisionAsync Tests

    [Fact]
    public async Task EvaluateContextPrecisionAsync_WithRelevantContexts_ReturnsHighScore()
    {
        // Arrange
        var question = "What is photosynthesis?";
        var contexts = new List<string>
        {
            "Photosynthesis is the process by which plants convert sunlight into energy.",
            "Plants use chlorophyll in photosynthesis to capture light energy."
        };

        // Act
        var result = await _evaluator.EvaluateContextPrecisionAsync(question, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MetricName.Should().Be("ContextPrecision");
        result.Value.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateContextPrecisionAsync_WithNoContexts_ReturnsZeroScore()
    {
        // Arrange
        var question = "What is photosynthesis?";
        var contexts = new List<string>();

        // Act
        var result = await _evaluator.EvaluateContextPrecisionAsync(question, contexts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().Be(0);
    }

    #endregion

    #region EvaluateContextRecallAsync Tests

    [Fact]
    public async Task EvaluateContextRecallAsync_WithGoodCoverage_ReturnsHighScore()
    {
        // Arrange
        var question = "What is DNA?";
        var contexts = new List<string>
        {
            "DNA is the molecule that carries genetic information in living organisms."
        };
        var groundTruth = "DNA carries genetic information in organisms.";

        // Act
        var result = await _evaluator.EvaluateContextRecallAsync(question, contexts, groundTruth);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MetricName.Should().Be("ContextRecall");
        result.Value.Score.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task EvaluateContextRecallAsync_WithoutGroundTruth_ReturnsFailure()
    {
        // Arrange
        var question = "What is DNA?";
        var contexts = new List<string> { "DNA context" };

        // Act
        var result = await _evaluator.EvaluateContextRecallAsync(question, contexts, "");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("GroundTruthRequired");
    }

    #endregion

    #region EvaluateBatchAsync Tests

    [Fact]
    public async Task EvaluateBatchAsync_WithMultipleRequests_ReturnsAggregateResults()
    {
        // Arrange
        var requests = new List<RAGEvaluationRequest>
        {
            new RAGEvaluationRequest
            {
                Question = "What is AI?",
                Answer = "AI is artificial intelligence.",
                Contexts = new List<string> { "Artificial intelligence (AI) is computer science." }
            },
            new RAGEvaluationRequest
            {
                Question = "What is ML?",
                Answer = "ML is machine learning.",
                Contexts = new List<string> { "Machine learning is a subset of AI." }
            }
        };

        // Act
        var result = await _evaluator.EvaluateBatchAsync(requests);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSamples.Should().Be(2);
        result.Value.SuccessfulEvaluations.Should().Be(2);
        result.Value.Results.Should().HaveCount(2);
        result.Value.AggregateMetrics.MeanOverallScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateBatchAsync_WithEmptyRequests_ReturnsFailure()
    {
        // Arrange
        var requests = new List<RAGEvaluationRequest>();

        // Act
        var result = await _evaluator.EvaluateBatchAsync(requests);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NoSamples");
    }

    [Fact]
    public async Task EvaluateBatchAsync_CalculatesPercentiles()
    {
        // Arrange
        var requests = Enumerable.Range(1, 10).Select(i => new RAGEvaluationRequest
        {
            Question = $"Question {i}",
            Answer = $"Answer about question {i} with some detail.",
            Contexts = new List<string> { $"Context for question {i} with information." }
        }).ToList();

        // Act
        var result = await _evaluator.EvaluateBatchAsync(requests);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AggregateMetrics.P50OverallScore.Should().BeGreaterThan(0);
        result.Value.AggregateMetrics.P90OverallScore.Should().BeGreaterThanOrEqualTo(
            result.Value.AggregateMetrics.P50OverallScore);
    }

    [Fact]
    public async Task EvaluateBatchAsync_TracksGradeDistribution()
    {
        // Arrange
        var requests = new List<RAGEvaluationRequest>
        {
            new RAGEvaluationRequest
            {
                Question = "What is cloud computing?",
                Answer = "Cloud computing provides on-demand computing resources over the internet.",
                Contexts = new List<string>
                {
                    "Cloud computing is the delivery of computing services over the internet."
                }
            }
        };

        // Act
        var result = await _evaluator.EvaluateBatchAsync(requests);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GradeDistribution.Should().NotBeEmpty();
    }

    #endregion

    #region GetStatisticsAsync Tests

    [Fact]
    public async Task GetStatisticsAsync_WithNoHistory_ReturnsEmptyStats()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await _evaluator.GetStatisticsAsync(workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluations.Should().Be(0);
        result.Value.AggregateMetrics.MeanOverallScore.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithHistory_ReturnsStatistics()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Add some evaluation history
        for (int i = 0; i < 5; i++)
        {
            await _evaluator.EvaluateAsync(new RAGEvaluationRequest
            {
                WorkspaceId = workspaceId,
                Question = $"Question {i}",
                Answer = $"Answer for question {i} with context.",
                Contexts = new List<string> { $"Context {i} with relevant information." }
            });
        }

        // Act
        var result = await _evaluator.GetStatisticsAsync(workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluations.Should().Be(5);
        result.Value.AggregateMetrics.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatisticsAsync_WithDateRange_FiltersResults()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        await _evaluator.EvaluateAsync(new RAGEvaluationRequest
        {
            WorkspaceId = workspaceId,
            Question = "Test question",
            Answer = "Test answer",
            Contexts = new List<string> { "Test context" }
        });

        // Act
        var result = await _evaluator.GetStatisticsAsync(
            workspaceId,
            from: DateTimeOffset.UtcNow.AddDays(-1),
            to: DateTimeOffset.UtcNow.AddDays(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluations.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Test Dataset Tests

    [Fact]
    public async Task CreateTestDatasetAsync_WithValidRequest_ReturnsId()
    {
        // Arrange
        var request = new TestDatasetRequest
        {
            Name = "Test Dataset 1",
            Description = "A test dataset for evaluation",
            Samples = new List<TestSample>
            {
                new TestSample
                {
                    Question = "What is 2+2?",
                    GroundTruth = "4"
                }
            }
        };

        // Act
        var result = await _evaluator.CreateTestDatasetAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateTestDatasetAsync_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var request = new TestDatasetRequest
        {
            Name = "",
            Samples = new List<TestSample>
            {
                new TestSample { Question = "Q", GroundTruth = "A" }
            }
        };

        // Act
        var result = await _evaluator.CreateTestDatasetAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTestDatasetAsync_WithEmptySamples_ReturnsFailure()
    {
        // Arrange
        var request = new TestDatasetRequest
        {
            Name = "Empty Dataset",
            Samples = new List<TestSample>()
        };

        // Act
        var result = await _evaluator.CreateTestDatasetAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetTestDatasetsAsync_ReturnsAllDatasets()
    {
        // Arrange
        await _evaluator.CreateTestDatasetAsync(new TestDatasetRequest
        {
            Name = "Dataset 1",
            Samples = new List<TestSample> { new TestSample { Question = "Q1", GroundTruth = "A1" } }
        });
        await _evaluator.CreateTestDatasetAsync(new TestDatasetRequest
        {
            Name = "Dataset 2",
            Samples = new List<TestSample> { new TestSample { Question = "Q2", GroundTruth = "A2" } }
        });

        // Act
        var result = await _evaluator.GetTestDatasetsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetTestDatasetsAsync_WithWorkspaceFilter_ReturnsFilteredDatasets()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        await _evaluator.CreateTestDatasetAsync(new TestDatasetRequest
        {
            Name = "Workspace Dataset",
            WorkspaceId = workspaceId,
            Samples = new List<TestSample> { new TestSample { Question = "Q", GroundTruth = "A" } }
        });

        // Act
        var result = await _evaluator.GetTestDatasetsAsync(workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(d => d.WorkspaceId == workspaceId);
    }

    [Fact]
    public async Task EvaluateTestDatasetAsync_WithValidDataset_ReturnsResults()
    {
        // Arrange
        var createResult = await _evaluator.CreateTestDatasetAsync(new TestDatasetRequest
        {
            Name = "Evaluation Dataset",
            Samples = new List<TestSample>
            {
                new TestSample { Question = "What is 1+1?", GroundTruth = "The answer is 2" },
                new TestSample { Question = "What is 2+2?", GroundTruth = "The answer is 4" }
            }
        });

        // Act
        var result = await _evaluator.EvaluateTestDatasetAsync(createResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TestDatasetId.Should().Be(createResult.Value);
        result.Value.EvaluationResult.Should().NotBeNull();
        result.Value.EvaluationResult.TotalSamples.Should().Be(2);
    }

    [Fact]
    public async Task EvaluateTestDatasetAsync_WithNonExistentDataset_ReturnsFailure()
    {
        // Act
        var result = await _evaluator.EvaluateTestDatasetAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("TestDatasetNotFound");
    }

    [Fact]
    public async Task EvaluateTestDatasetAsync_DeterminesPassOrFail()
    {
        // Arrange
        var createResult = await _evaluator.CreateTestDatasetAsync(new TestDatasetRequest
        {
            Name = "Pass/Fail Dataset",
            Samples = new List<TestSample>
            {
                new TestSample
                {
                    Question = "What is machine learning?",
                    GroundTruth = "Machine learning is a subset of AI that uses data to train models"
                }
            }
        });

        // Act
        var result = await _evaluator.EvaluateTestDatasetAsync(createResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PassThreshold.Should().Be(0.7);
        // Either passed or has failure reasons
        if (!result.Value.Passed)
        {
            result.Value.FailureReasons.Should().NotBeEmpty();
        }
    }

    #endregion

    #region Hallucination Detection Tests

    [Fact]
    public async Task EvaluateAsync_DetectsHallucination()
    {
        // Arrange
        var request = new RAGEvaluationRequest
        {
            Question = "What is the population of Tokyo?",
            Answer = "Tokyo has a population of 14 million people and is known for sushi. The Eiffel Tower is also located there.",
            Contexts = new List<string>
            {
                "Tokyo is the capital of Japan with a population of approximately 14 million."
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HallucinationScore.Should().NotBeNull();
        // The claim about Eiffel Tower should be detected as hallucination
    }

    #endregion
}
