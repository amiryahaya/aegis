using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.NER;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.NER;

public class BasicNERServiceTests
{
    private readonly BasicNERService _service;

    public BasicNERServiceTests()
    {
        _service = new BasicNERService(NullLogger<BasicNERService>.Instance);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithEmail_ShouldDetectEmailEntity()
    {
        // Arrange
        var text = "Please contact john.doe@example.com for more information.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(e => e.Type == EntityType.Email);
        result.Value.First(e => e.Type == EntityType.Email).Text.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithUrl_ShouldDetectUrlEntity()
    {
        // Arrange
        var text = "Visit our website at https://www.example.com for more details.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(e => e.Type == EntityType.Url);
        result.Value.First(e => e.Type == EntityType.Url).Text.Should().Contain("example.com");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithIpAddress_ShouldDetectIpEntity()
    {
        // Arrange
        var text = "The suspicious activity originated from 192.168.1.100 at 10:30 AM.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(e => e.Type == EntityType.IpAddress);
        result.Value.First(e => e.Type == EntityType.IpAddress).Text.Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithPhoneNumber_ShouldDetectPhoneEntity()
    {
        // Arrange
        var text = "Call us at +1-555-123-4567 or (555) 987-6543 for support.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Type == EntityType.PhoneNumber);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithMultipleEntities_ShouldDetectAll()
    {
        // Arrange
        var text = @"
Contact: john@example.com
Website: https://example.com
IP: 10.0.0.1
Phone: +1-555-0100
";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterOrEqualTo(4);
        result.Value.Should().Contain(e => e.Type == EntityType.Email);
        result.Value.Should().Contain(e => e.Type == EntityType.Url);
        result.Value.Should().Contain(e => e.Type == EntityType.IpAddress);
        result.Value.Should().Contain(e => e.Type == EntityType.PhoneNumber);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithNoEntities_ShouldReturnEmptyList()
    {
        // Arrange
        var text = "This is plain text with no special entities.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithNullText_ShouldReturnFailure()
    {
        // Act
        var result = await _service.ExtractEntitiesAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ShouldIncludePositionInformation()
    {
        // Arrange
        var text = "Email: test@example.com is valid.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var emailEntity = result.Value.First(e => e.Type == EntityType.Email);
        emailEntity.StartPosition.Should().BeGreaterOrEqualTo(0);
        emailEntity.EndPosition.Should().BeGreaterThan(emailEntity.StartPosition);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ShouldIncludeConfidenceScore()
    {
        // Arrange
        var text = "Contact admin@company.com for help.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var emailEntity = result.Value.First(e => e.Type == EntityType.Email);
        emailEntity.Confidence.Should().BeInRange(0.0, 1.0);
        emailEntity.Confidence.Should().BeGreaterThan(0.5); // Pattern-based should have high confidence
    }
}
