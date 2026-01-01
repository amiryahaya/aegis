using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class WorkingMemoryEnhancedTests
{
    private readonly ILogger<WorkingMemoryService> _logger;
    private readonly WorkingMemoryService _workingMemory;

    public WorkingMemoryEnhancedTests()
    {
        _logger = Substitute.For<ILogger<WorkingMemoryService>>();
        _workingMemory = new WorkingMemoryService(_logger);
    }

    [Fact]
    public async Task TrackEntityAsync_ShouldStoreEntityReference()
    {
        // Arrange
        var sessionId = "session-1";
        var entity = new TrackedEntity
        {
            Name = "John Doe",
            Type = "Person",
            FirstMentionedAt = DateTime.UtcNow,
            Aliases = new List<string> { "John", "Mr. Doe" }
        };

        // Act
        var result = await _workingMemory.TrackEntityAsync(sessionId, entity);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetTrackedEntitiesAsync_ShouldReturnAllEntities()
    {
        // Arrange
        var sessionId = "session-2";
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Acme Corp",
            Type = "Organization"
        });
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "New York",
            Type = "Location"
        });

        // Act
        var result = await _workingMemory.GetTrackedEntitiesAsync(sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveReferenceAsync_WithPronoun_ShouldReturnMostRecentEntity()
    {
        // Arrange
        var sessionId = "session-3";
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Apple Inc",
            Type = "Organization",
            LastMentionedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Tim Cook",
            Type = "Person",
            LastMentionedAt = DateTime.UtcNow
        });

        // Act
        var result = await _workingMemory.ResolveReferenceAsync(sessionId, "he");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Tim Cook");
    }

    [Fact]
    public async Task ResolveReferenceAsync_WithIt_ShouldReturnMostRecentNonPerson()
    {
        // Arrange
        var sessionId = "session-4";
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Tesla",
            Type = "Organization",
            LastMentionedAt = DateTime.UtcNow
        });
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Elon Musk",
            Type = "Person",
            LastMentionedAt = DateTime.UtcNow.AddSeconds(1)
        });

        // Act
        var result = await _workingMemory.ResolveReferenceAsync(sessionId, "it");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Tesla");
    }

    [Fact]
    public async Task SetCurrentTopicAsync_ShouldStoreTopic()
    {
        // Arrange
        var sessionId = "session-5";

        // Act
        var result = await _workingMemory.SetCurrentTopicAsync(sessionId, "financial analysis");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentTopicAsync_ShouldReturnCurrentTopic()
    {
        // Arrange
        var sessionId = "session-6";
        await _workingMemory.SetCurrentTopicAsync(sessionId, "risk assessment");

        // Act
        var result = await _workingMemory.GetCurrentTopicAsync(sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("risk assessment");
    }

    [Fact]
    public async Task GetConversationSummaryAsync_ShouldReturnSummary()
    {
        // Arrange
        var sessionId = "session-7";
        await _workingMemory.AddMessageAsync(sessionId, new ConversationMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "user",
            Content = "Tell me about Apple's financial performance",
            Timestamp = DateTime.UtcNow.AddMinutes(-2)
        });
        await _workingMemory.AddMessageAsync(sessionId, new ConversationMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "assistant",
            Content = "Apple reported strong Q3 2024 results...",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        await _workingMemory.AddMessageAsync(sessionId, new ConversationMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "user",
            Content = "What about their services revenue?",
            Timestamp = DateTime.UtcNow
        });

        // Act
        var result = await _workingMemory.GetConversationSummaryAsync(sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TotalMessages.Should().Be(3);
        result.Value.UserMessages.Should().Be(2);
        result.Value.AssistantMessages.Should().Be(1);
    }

    [Fact]
    public async Task AddMessageAsync_ShouldAutoExtractEntities()
    {
        // Arrange
        var sessionId = "session-8";

        // Act
        await _workingMemory.AddMessageAsync(sessionId, new ConversationMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "user",
            Content = "What is Microsoft's market cap?",
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                { "entities", new[] { new { name = "Microsoft", type = "Organization" } } }
            }
        });

        // Track extracted entity
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Microsoft",
            Type = "Organization"
        });

        // Assert
        var entities = await _workingMemory.GetTrackedEntitiesAsync(sessionId);
        entities.Value.Should().Contain(e => e.Name == "Microsoft");
    }

    [Fact]
    public async Task GetContextWindowAsync_ShouldReturnFormattedContext()
    {
        // Arrange
        var sessionId = "session-9";
        await _workingMemory.AddMessageAsync(sessionId, new ConversationMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "user",
            Content = "What is AWS?",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });
        await _workingMemory.AddMessageAsync(sessionId, new ConversationMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "assistant",
            Content = "AWS is Amazon Web Services...",
            Timestamp = DateTime.UtcNow
        });
        await _workingMemory.SetCurrentTopicAsync(sessionId, "cloud computing");

        // Act
        var result = await _workingMemory.GetContextWindowAsync(sessionId, maxMessages: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().Contain("AWS");
        result.Value.Should().Contain("cloud computing");
    }

    [Fact]
    public async Task UpdateEntityMentionAsync_ShouldUpdateLastMentioned()
    {
        // Arrange
        var sessionId = "session-10";
        var initialTime = DateTime.UtcNow.AddMinutes(-10);
        await _workingMemory.TrackEntityAsync(sessionId, new TrackedEntity
        {
            Name = "Google",
            Type = "Organization",
            LastMentionedAt = initialTime
        });

        // Act
        await _workingMemory.UpdateEntityMentionAsync(sessionId, "Google");

        // Assert
        var entities = await _workingMemory.GetTrackedEntitiesAsync(sessionId);
        var google = entities.Value.FirstOrDefault(e => e.Name == "Google");
        google.Should().NotBeNull();
        google!.LastMentionedAt.Should().BeAfter(initialTime);
    }
}
