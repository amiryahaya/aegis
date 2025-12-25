using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.NER;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.NER;

public class IntelligenceNERServiceTests
{
    private readonly IntelligenceNERService _service;

    public IntelligenceNERServiceTests()
    {
        var basicNER = new BasicNERService(NullLogger<BasicNERService>.Instance);
        _service = new IntelligenceNERService(basicNER, NullLogger<IntelligenceNERService>.Instance);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithCVE_ShouldDetectCVEEntity()
    {
        // Arrange
        var text = "The vulnerability CVE-2021-44228 (Log4Shell) affects millions of systems.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Text == "CVE-2021-44228");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithMD5Hash_ShouldDetectHash()
    {
        // Arrange
        var text = "Malware hash: 5d41402abc4b2a76b9719d911017c592";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Text == "5d41402abc4b2a76b9719d911017c592");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithSHA256Hash_ShouldDetectHash()
    {
        // Arrange
        var text = "SHA256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e =>
            e.Text == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithMITRETechnique_ShouldDetectTechnique()
    {
        // Arrange
        var text = "The attacker used T1059.001 (PowerShell) to execute malicious code.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Text.Contains("T1059"));
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithMultipleIntelEntities_ShouldDetectAll()
    {
        // Arrange
        var text = @"
Threat Report:
- CVE-2023-12345 exploited
- Hash: 5d41402abc4b2a76b9719d911017c592
- Technique: T1566 (Phishing)
- Contact: analyst@security.com
";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should have CVE, hash, technique, and email
        result.Value.Should().Contain(e => e.Text.Contains("CVE"));
        result.Value.Should().Contain(e => e.Text == "5d41402abc4b2a76b9719d911017c592");
        result.Value.Should().Contain(e => e.Text.Contains("T1566"));
        result.Value.Should().Contain(e => e.Type == EntityType.Email);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ShouldIncludeBasicEntities()
    {
        // Arrange
        var text = "Contact security@company.com or visit https://company.com for CVE-2023-0001 details.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should include both basic (email, URL) and intel (CVE) entities
        result.Value.Should().Contain(e => e.Type == EntityType.Email);
        result.Value.Should().Contain(e => e.Type == EntityType.Url);
        result.Value.Should().Contain(e => e.Text.Contains("CVE"));
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithDomainName_ShouldDetectDomain()
    {
        // Arrange
        var text = "Malicious domain: evil-site.malware.com was used in the campaign.";

        // Act
        var result = await _service.ExtractEntitiesAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Text.Contains("evil-site.malware.com"));
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WithNullText_ShouldReturnFailure()
    {
        // Act
        var result = await _service.ExtractEntitiesAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
