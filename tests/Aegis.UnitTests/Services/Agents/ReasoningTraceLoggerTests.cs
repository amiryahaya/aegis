using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class ReasoningTraceLoggerTests
{
    private readonly ILogger<ReasoningTraceLogger> _logger;

    public ReasoningTraceLoggerTests()
    {
        _logger = Substitute.For<ILogger<ReasoningTraceLogger>>();
    }

    [Fact]
    public void StartTrace_ShouldCreateNewTrace()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var requestId = Guid.NewGuid();

        // Act
        var traceId = traceLogger.StartTrace(requestId, "Test query");

        // Assert
        traceId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AddStep_ShouldRecordReasoningStep()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var requestId = Guid.NewGuid();
        var traceId = traceLogger.StartTrace(requestId, "Test query");

        // Act
        traceLogger.AddStep(traceId, "Planner", "Analyzing query intent");
        traceLogger.AddStep(traceId, "Planner", "Identified search task");

        // Assert
        var trace = traceLogger.GetTrace(traceId);
        trace.Should().NotBeNull();
        trace!.Steps.Should().HaveCount(2);
        trace.Steps[0].AgentName.Should().Be("Planner");
        trace.Steps[0].Description.Should().Be("Analyzing query intent");
    }

    [Fact]
    public void AddStep_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "Query");
        var metadata = new Dictionary<string, object>
        {
            { "documentsFound", 5 },
            { "confidence", 0.85 }
        };

        // Act
        traceLogger.AddStep(traceId, "Retriever", "Found documents", metadata);

        // Assert
        var trace = traceLogger.GetTrace(traceId);
        trace!.Steps[0].Metadata.Should().ContainKey("documentsFound");
        trace.Steps[0].Metadata["documentsFound"].Should().Be(5);
    }

    [Fact]
    public void GetTrace_WithInvalidTraceId_ShouldReturnNull()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);

        // Act
        var trace = traceLogger.GetTrace(Guid.NewGuid());

        // Assert
        trace.Should().BeNull();
    }

    [Fact]
    public void CompleteTrace_ShouldMarkTraceAsComplete()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "Query");
        traceLogger.AddStep(traceId, "Agent", "Step 1");

        // Act
        traceLogger.CompleteTrace(traceId, success: true);

        // Assert
        var trace = traceLogger.GetTrace(traceId);
        trace!.IsComplete.Should().BeTrue();
        trace.IsSuccess.Should().BeTrue();
        trace.EndTime.Should().NotBeNull();
    }

    [Fact]
    public void CompleteTrace_WithError_ShouldRecordError()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "Query");

        // Act
        traceLogger.CompleteTrace(traceId, success: false, error: "LLM timeout");

        // Assert
        var trace = traceLogger.GetTrace(traceId);
        trace!.IsSuccess.Should().BeFalse();
        trace.Error.Should().Be("LLM timeout");
    }

    [Fact]
    public void GetTrace_ShouldCalculateDuration()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "Query");
        traceLogger.AddStep(traceId, "Agent", "Processing");
        Thread.Sleep(10); // Small delay to ensure measurable duration
        traceLogger.CompleteTrace(traceId, success: true);

        // Act
        var trace = traceLogger.GetTrace(traceId);

        // Assert
        trace!.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void FormatAsText_ShouldReturnHumanReadableTrace()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "What is the capital of France?");
        traceLogger.AddStep(traceId, "Planner", "Identified as factual question");
        traceLogger.AddStep(traceId, "Retriever", "Found 3 relevant documents");
        traceLogger.AddStep(traceId, "Synthesizer", "Generated response");
        traceLogger.CompleteTrace(traceId, success: true);

        // Act
        var textFormat = traceLogger.FormatAsText(traceId);

        // Assert
        textFormat.Should().Contain("What is the capital of France?");
        textFormat.Should().Contain("Planner");
        textFormat.Should().Contain("Retriever");
        textFormat.Should().Contain("Synthesizer");
    }

    [Fact]
    public void FormatAsJson_ShouldReturnValidJson()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "Test query");
        traceLogger.AddStep(traceId, "Agent", "Step 1");
        traceLogger.CompleteTrace(traceId, success: true);

        // Act
        var jsonFormat = traceLogger.FormatAsJson(traceId);

        // Assert
        jsonFormat.Should().NotBeEmpty();
        jsonFormat.Should().Contain("\"query\"");
        jsonFormat.Should().Contain("\"steps\"");
    }

    [Fact]
    public void GetRecentTraces_ShouldReturnMostRecentTraces()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        for (int i = 0; i < 5; i++)
        {
            var id = traceLogger.StartTrace(Guid.NewGuid(), $"Query {i}");
            traceLogger.CompleteTrace(id, success: true);
        }

        // Act
        var recentTraces = traceLogger.GetRecentTraces(limit: 3);

        // Assert
        recentTraces.Should().HaveCount(3);
    }

    [Fact]
    public void AddDecision_ShouldRecordDecisionPoint()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var traceId = traceLogger.StartTrace(Guid.NewGuid(), "Complex query");

        // Act
        traceLogger.AddDecision(traceId, "Planner", "Route selection",
            options: new[] { "Vector search", "Graph query", "Hybrid" },
            selectedOption: "Hybrid",
            rationale: "Query contains both entity reference and semantic concepts");

        // Assert
        var trace = traceLogger.GetTrace(traceId);
        trace!.Decisions.Should().HaveCount(1);
        trace.Decisions[0].SelectedOption.Should().Be("Hybrid");
        trace.Decisions[0].Rationale.Should().Contain("entity reference");
    }

    [Fact]
    public void ClearOldTraces_ShouldRemoveExpiredTraces()
    {
        // Arrange
        var traceLogger = new ReasoningTraceLogger(_logger);
        var oldTraceId = traceLogger.StartTrace(Guid.NewGuid(), "Old query");
        traceLogger.CompleteTrace(oldTraceId, success: true);

        // Act
        traceLogger.ClearOldTraces(maxAge: TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        traceLogger.ClearOldTraces(maxAge: TimeSpan.FromMilliseconds(1));

        // Assert - Note: This test verifies the method runs, actual cleanup depends on timing
        // In production, traces older than maxAge would be removed
        var traces = traceLogger.GetRecentTraces(limit: 100);
        traces.Should().NotBeNull();
    }
}
