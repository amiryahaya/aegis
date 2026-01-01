using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Webhooks;

public class EventPublisherTests
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<InMemoryEventPublisher> _logger;
    private readonly InMemoryEventPublisher _sut;

    public EventPublisherTests()
    {
        _webhookService = Substitute.For<IWebhookService>();
        _logger = Substitute.For<ILogger<InMemoryEventPublisher>>();
        _sut = new InMemoryEventPublisher(_webhookService, _logger);
    }

    [Fact]
    public async Task PublishAsync_WithNoTeamId_ShouldNotCallWebhookService()
    {
        // Arrange
        var domainEvent = new TestWebhookEvent();

        // Act
        var result = await _sut.PublishAsync(domainEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _webhookService.DidNotReceive()
            .DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WithTeamId_ShouldCallWebhookService()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var domainEvent = new DocumentUploadedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            TeamId = teamId
        };

        _webhookService.DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<WebhookDeliveryResult>.Success(new WebhookDeliveryResult
            {
                EventId = Guid.CreateVersion7(),
                TotalSubscriptions = 1,
                SuccessfulDeliveries = 1
            }));

        // Act
        var result = await _sut.PublishAsync(domainEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _webhookService.Received(1)
            .DeliverEventAsync(Arg.Is<WebhookEvent>(e =>
                e.Type == WebhookEventType.DocumentUploaded &&
                e.TeamId == teamId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WhenWebhookDeliveryFails_ShouldReturnFailure()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var domainEvent = new DocumentUploadedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            TeamId = teamId
        };

        _webhookService.DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<WebhookDeliveryResult>.Failure(
                Error.Internal("Webhook.DeliveryFailed", "Failed")));

        // Act
        var result = await _sut.PublishAsync(domainEvent);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PublishManyAsync_ShouldPublishAllEvents()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var events = new DomainEvent[]
        {
            new DocumentUploadedEvent
            {
                DocumentId = Guid.CreateVersion7(),
                FileName = "test1.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                TeamId = teamId
            },
            new DocumentUploadedEvent
            {
                DocumentId = Guid.CreateVersion7(),
                FileName = "test2.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                TeamId = teamId
            }
        };

        _webhookService.DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<WebhookDeliveryResult>.Success(new WebhookDeliveryResult
            {
                EventId = Guid.CreateVersion7(),
                TotalSubscriptions = 1,
                SuccessfulDeliveries = 1
            }));

        // Act
        var result = await _sut.PublishManyAsync(events);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _webhookService.Received(2)
            .DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishManyAsync_WithEmptyList_ShouldReturnSuccess()
    {
        // Act
        var result = await _sut.PublishManyAsync([]);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_ShouldInvokeHandlerOnPublish()
    {
        // Arrange
        var handlerCalled = false;
        DocumentUploadedEvent? receivedEvent = null;

        _sut.Subscribe<DocumentUploadedEvent>(async (e, ct) =>
        {
            handlerCalled = true;
            receivedEvent = e;
            await Task.CompletedTask;
        });

        var domainEvent = new DocumentUploadedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024
        };

        // Act
        await _sut.PublishAsync(domainEvent);

        // Assert
        handlerCalled.Should().BeTrue();
        receivedEvent.Should().NotBeNull();
        receivedEvent!.FileName.Should().Be("test.pdf");
    }

    [Fact]
    public async Task Subscribe_WithMultipleHandlers_ShouldInvokeAll()
    {
        // Arrange
        var handler1Called = false;
        var handler2Called = false;

        _sut.Subscribe<DocumentUploadedEvent>(async (e, ct) =>
        {
            handler1Called = true;
            await Task.CompletedTask;
        });

        _sut.Subscribe<DocumentUploadedEvent>(async (e, ct) =>
        {
            handler2Called = true;
            await Task.CompletedTask;
        });

        var domainEvent = new DocumentUploadedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024
        };

        // Act
        await _sut.PublishAsync(domainEvent);

        // Assert
        handler1Called.Should().BeTrue();
        handler2Called.Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_ShouldStopInvokingHandler()
    {
        // Arrange
        var handlerCallCount = 0;

        Func<DocumentUploadedEvent, CancellationToken, Task> handler = async (e, ct) =>
        {
            handlerCallCount++;
            await Task.CompletedTask;
        };

        _sut.Subscribe(handler);

        var domainEvent = new DocumentUploadedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024
        };

        // Publish first event
        await _sut.PublishAsync(domainEvent);

        // Unsubscribe
        _sut.Unsubscribe(handler);

        // Publish second event
        await _sut.PublishAsync(domainEvent);

        // Assert - handler should only have been called once
        handlerCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Subscribe_WhenHandlerThrows_ShouldReturnPartialFailure()
    {
        // Arrange
        _sut.Subscribe<DocumentUploadedEvent>((e, ct) =>
            throw new InvalidOperationException("Handler error"));

        var domainEvent = new DocumentUploadedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024
        };

        // Act
        var result = await _sut.PublishAsync(domainEvent);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("PublishPartialFailure");
    }

    [Fact]
    public void Constructor_WithNullWebhookService_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryEventPublisher(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("webhookService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryEventPublisher(_webhookService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_ShouldThrow()
    {
        // Act
        var act = async () => await _sut.PublishAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishManyAsync_WithNullEvents_ShouldThrow()
    {
        // Act
        var act = async () => await _sut.PublishManyAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Subscribe_WithNullHandler_ShouldThrow()
    {
        // Act
        var act = () => _sut.Subscribe<DocumentUploadedEvent>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unsubscribe_WithNullHandler_ShouldThrow()
    {
        // Act
        var act = () => _sut.Unsubscribe<DocumentUploadedEvent>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_DocumentProcessedEvent_ShouldCreateCorrectPayload()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var domainEvent = new DocumentProcessedEvent
        {
            DocumentId = Guid.CreateVersion7(),
            FileName = "test.pdf",
            ChunkCount = 10,
            TokenCount = 5000,
            ProcessingTime = TimeSpan.FromSeconds(5),
            TeamId = teamId
        };

        _webhookService.DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<WebhookDeliveryResult>.Success(new WebhookDeliveryResult
            {
                EventId = Guid.CreateVersion7(),
                TotalSubscriptions = 0
            }));

        // Act
        await _sut.PublishAsync(domainEvent);

        // Assert
        await _webhookService.Received(1)
            .DeliverEventAsync(Arg.Is<WebhookEvent>(e =>
                e.Type == WebhookEventType.DocumentProcessed &&
                e.TeamId == teamId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_QueryCompletedEvent_ShouldCreateCorrectPayload()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var domainEvent = new QueryCompletedEvent
        {
            QueryId = Guid.CreateVersion7(),
            Query = "What is the capital of France?",
            SourceCount = 5,
            ProcessingTime = TimeSpan.FromMilliseconds(250),
            ConfidenceScore = 0.95,
            CacheHit = true,
            TeamId = teamId
        };

        _webhookService.DeliverEventAsync(Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<WebhookDeliveryResult>.Success(new WebhookDeliveryResult
            {
                EventId = Guid.CreateVersion7(),
                TotalSubscriptions = 0
            }));

        // Act
        await _sut.PublishAsync(domainEvent);

        // Assert
        await _webhookService.Received(1)
            .DeliverEventAsync(Arg.Is<WebhookEvent>(e =>
                e.Type == WebhookEventType.QueryCompleted &&
                e.TeamId == teamId), Arg.Any<CancellationToken>());
    }
}
