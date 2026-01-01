using System.Net;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Aegis.UnitTests.Services.Webhooks;

public class WebhookServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InMemoryWebhookService> _logger;
    private readonly InMemoryWebhookService _sut;

    public WebhookServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient("WebhookDelivery")
            .Returns(_mockHttp.ToHttpClient());
        _logger = Substitute.For<ILogger<InMemoryWebhookService>>();
        _sut = new InMemoryWebhookService(_httpClientFactory, _logger);
    }

    [Fact]
    public async Task RegisterAsync_WithValidRegistration_ShouldReturnSuccess()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded, WebhookEventType.DocumentProcessed]
        };

        // Act
        var result = await _sut.RegisterAsync(registration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be("Test Webhook");
        result.Value.Url.Should().Be("https://example.com/webhook");
        result.Value.Events.Should().HaveCount(2);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_WithEmptyUrl_ShouldReturnFailure()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Test Webhook",
            Url = "",
            Events = [WebhookEventType.DocumentUploaded]
        };

        // Act
        var result = await _sut.RegisterAsync(registration);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidUrl");
    }

    [Fact]
    public async Task RegisterAsync_WithInvalidUrl_ShouldReturnFailure()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Test Webhook",
            Url = "ftp://invalid.com",
            Events = [WebhookEventType.DocumentUploaded]
        };

        // Act
        var result = await _sut.RegisterAsync(registration);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidUrl");
    }

    [Fact]
    public async Task RegisterAsync_WithNoEvents_ShouldReturnFailure()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = []
        };

        // Act
        var result = await _sut.RegisterAsync(registration);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NoEvents");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnWebhook()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded]
        };
        var registerResult = await _sut.RegisterAsync(registration);

        // Act
        var result = await _sut.GetByIdAsync(registerResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Webhook");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.GetByIdAsync(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task UpdateAsync_WhenExists_ShouldUpdateWebhook()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Original Name",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded]
        };
        var registerResult = await _sut.RegisterAsync(registration);

        var update = new WebhookUpdate
        {
            Name = "Updated Name",
            IsActive = false
        };

        // Act
        var result = await _sut.UpdateAsync(registerResult.Value.Id, update);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Name");
        result.Value.IsActive.Should().BeFalse();
        result.Value.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotExists_ShouldReturnNotFound()
    {
        // Arrange
        var update = new WebhookUpdate { Name = "Updated Name" };

        // Act
        var result = await _sut.UpdateAsync(Guid.CreateVersion7(), update);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldRemoveWebhook()
    {
        // Arrange
        var registration = new WebhookRegistration
        {
            TeamId = Guid.CreateVersion7(),
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded]
        };
        var registerResult = await _sut.RegisterAsync(registration);

        // Act
        var deleteResult = await _sut.DeleteAsync(registerResult.Value.Id);
        var getResult = await _sut.GetByIdAsync(registerResult.Value.Id);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        getResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.DeleteAsync(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task ListByTeamAsync_ShouldReturnOnlyTeamWebhooks()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var otherTeamId = Guid.CreateVersion7();

        await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Team Webhook 1",
            Url = "https://example.com/webhook1",
            Events = [WebhookEventType.DocumentUploaded]
        });

        await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Team Webhook 2",
            Url = "https://example.com/webhook2",
            Events = [WebhookEventType.DocumentProcessed]
        });

        await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = otherTeamId,
            Name = "Other Team Webhook",
            Url = "https://example.com/webhook3",
            Events = [WebhookEventType.DocumentUploaded]
        });

        // Act
        var result = await _sut.ListByTeamAsync(teamId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(w => w.TeamId.Should().Be(teamId));
    }

    [Fact]
    public async Task DeliverEventAsync_WhenNoSubscriptions_ShouldReturnEmptyResult()
    {
        // Arrange
        var webhookEvent = new WebhookEvent
        {
            Type = WebhookEventType.DocumentUploaded,
            TeamId = Guid.CreateVersion7(),
            Payload = new { test = "data" }
        };

        // Act
        var result = await _sut.DeliverEventAsync(webhookEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSubscriptions.Should().Be(0);
        result.Value.SuccessfulDeliveries.Should().Be(0);
    }

    [Fact]
    public async Task DeliverEventAsync_WhenSubscriptionMatches_ShouldDeliverEvent()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        _mockHttp.When("https://example.com/webhook")
            .Respond(HttpStatusCode.OK);

        await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded]
        });

        var webhookEvent = new WebhookEvent
        {
            Type = WebhookEventType.DocumentUploaded,
            TeamId = teamId,
            Payload = new { documentId = Guid.CreateVersion7() }
        };

        // Act
        var result = await _sut.DeliverEventAsync(webhookEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSubscriptions.Should().Be(1);
        result.Value.SuccessfulDeliveries.Should().Be(1);
    }

    [Fact]
    public async Task DeliverEventAsync_WhenEventTypeNotSubscribed_ShouldSkipDelivery()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentProcessed] // Not DocumentUploaded
        });

        var webhookEvent = new WebhookEvent
        {
            Type = WebhookEventType.DocumentUploaded,
            TeamId = teamId,
            Payload = new { documentId = Guid.CreateVersion7() }
        };

        // Act
        var result = await _sut.DeliverEventAsync(webhookEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task DeliverEventAsync_WhenWebhookInactive_ShouldSkipDelivery()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        var registerResult = await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded],
            IsActive = false
        });

        var webhookEvent = new WebhookEvent
        {
            Type = WebhookEventType.DocumentUploaded,
            TeamId = teamId,
            Payload = new { documentId = Guid.CreateVersion7() }
        };

        // Act
        var result = await _sut.DeliverEventAsync(webhookEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSubscriptions.Should().Be(0);
    }

    [Fact]
    public async Task DeliverEventAsync_WhenHttpFails_ShouldRecordFailure()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        _mockHttp.When("https://example.com/webhook")
            .Respond(HttpStatusCode.InternalServerError, "text/plain", "Server Error");

        await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded]
        });

        var webhookEvent = new WebhookEvent
        {
            Type = WebhookEventType.DocumentUploaded,
            TeamId = teamId,
            Payload = new { documentId = Guid.CreateVersion7() }
        };

        // Act
        var result = await _sut.DeliverEventAsync(webhookEvent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSubscriptions.Should().Be(1);
        result.Value.FailedDeliveries.Should().Be(1);
    }

    [Fact]
    public async Task TestAsync_WhenWebhookExists_ShouldDeliverTestEvent()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        _mockHttp.When("https://example.com/webhook")
            .Respond(HttpStatusCode.OK);

        var registerResult = await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.Test]
        });

        // Act
        var result = await _sut.TestAsync(registerResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be(WebhookEventType.Test);
        result.Value.Status.Should().Be(WebhookDeliveryStatus.Success);
    }

    [Fact]
    public async Task TestAsync_WhenWebhookNotExists_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.TestAsync(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task GetDeliveryHistoryAsync_ShouldReturnDeliveries()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();

        _mockHttp.When("https://example.com/webhook")
            .Respond(HttpStatusCode.OK);

        var registerResult = await _sut.RegisterAsync(new WebhookRegistration
        {
            TeamId = teamId,
            Name = "Test Webhook",
            Url = "https://example.com/webhook",
            Events = [WebhookEventType.DocumentUploaded]
        });

        // Deliver some events
        for (int i = 0; i < 3; i++)
        {
            await _sut.DeliverEventAsync(new WebhookEvent
            {
                Type = WebhookEventType.DocumentUploaded,
                TeamId = teamId,
                Payload = new { iteration = i }
            });
        }

        // Act
        var result = await _sut.GetDeliveryHistoryAsync(registerResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDeliveryHistoryAsync_WhenWebhookNotExists_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.GetDeliveryHistoryAsync(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public void VerifySignature_WithValidSignature_ShouldReturnTrue()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var secret = "my-secret-key";

        // Compute expected signature
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var expectedSignature = Convert.ToHexStringLower(hash);

        // Act
        var result = _sut.VerifySignature(payload, expectedSignature, secret);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_WithInvalidSignature_ShouldReturnFalse()
    {
        // Arrange
        var payload = "{\"test\":\"data\"}";
        var secret = "my-secret-key";

        // Act
        var result = _sut.VerifySignature(payload, "invalid-signature", secret);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_WithEmptyInputs_ShouldReturnFalse()
    {
        // Assert
        _sut.VerifySignature("", "sig", "secret").Should().BeFalse();
        _sut.VerifySignature("payload", "", "secret").Should().BeFalse();
        _sut.VerifySignature("payload", "sig", "").Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryWebhookService(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryWebhookService(_httpClientFactory, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
