using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.DocumentParsing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Aegis.UnitTests.Services.DocumentParsing;

public class HtmlDocumentParserTests
{
    private readonly HtmlDocumentParser _parser;

    public HtmlDocumentParserTests()
    {
        _parser = new HtmlDocumentParser(NullLogger<HtmlDocumentParser>.Instance);
    }

    [Fact]
    public void SupportedExtensions_ShouldIncludeHtmlAndHtm()
    {
        // Assert
        _parser.SupportedExtensions.Should().Contain(".html");
        _parser.SupportedExtensions.Should().Contain(".htm");
    }

    [Fact]
    public async Task ParseAsync_WithValidHtml_ShouldExtractTextContent()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Test Document</title>
</head>
<body>
    <h1>Welcome to Intelligence Analysis</h1>
    <p>This document contains important information about threat actors.</p>
    <div>
        <p>The analysis shows significant activity in the region.</p>
    </div>
</body>
</html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Welcome to Intelligence Analysis");
        result.Value.Text.Should().Contain("important information about threat actors");
        result.Value.Text.Should().Contain("significant activity in the region");
        result.Value.Text.Should().NotContain("<h1>");
        result.Value.Text.Should().NotContain("<p>");
        result.Value.Text.Should().NotContain("<div>");
    }

    [Fact]
    public async Task ParseAsync_ShouldStripScriptAndStyleTags()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<head>
    <style>body { color: red; }</style>
</head>
<body>
    <p>Visible content</p>
    <script>console.log('This should not appear');</script>
    <p>More visible content</p>
</body>
</html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Visible content");
        result.Value.Text.Should().Contain("More visible content");
        result.Value.Text.Should().NotContain("console.log");
        result.Value.Text.Should().NotContain("color: red");
    }

    [Fact]
    public async Task ParseAsync_WithMetadata_ShouldExtractTitleAndDescription()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Intelligence Report</title>
    <meta name=""description"" content=""Analysis of threat landscape"">
    <meta name=""author"" content=""Analyst Team"">
</head>
<body>
    <p>Report content</p>
</body>
</html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("Title");
        result.Value.Metadata["Title"].Should().Be("Intelligence Report");
        result.Value.Metadata.Should().ContainKey("Description");
        result.Value.Metadata["Description"].Should().Be("Analysis of threat landscape");
        result.Value.Metadata.Should().ContainKey("Author");
        result.Value.Metadata["Author"].Should().Be("Analyst Team");
    }

    [Fact]
    public async Task ParseAsync_WithLists_ShouldFormatCorrectly()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <h2>Threat Indicators:</h2>
    <ul>
        <li>Unusual network traffic</li>
        <li>Suspicious login attempts</li>
        <li>Data exfiltration</li>
    </ul>
    <ol>
        <li>First step</li>
        <li>Second step</li>
    </ol>
</body>
</html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Threat Indicators");
        result.Value.Text.Should().Contain("Unusual network traffic");
        result.Value.Text.Should().Contain("Suspicious login attempts");
        result.Value.Text.Should().Contain("Data exfiltration");
        result.Value.Text.Should().Contain("First step");
        result.Value.Text.Should().Contain("Second step");
    }

    [Fact]
    public async Task ParseAsync_WithTables_ShouldExtractTableData()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <table>
        <thead>
            <tr>
                <th>Indicator</th>
                <th>Severity</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>IP Address</td>
                <td>High</td>
            </tr>
            <tr>
                <td>Domain Name</td>
                <td>Medium</td>
            </tr>
        </tbody>
    </table>
</body>
</html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Indicator");
        result.Value.Text.Should().Contain("Severity");
        result.Value.Text.Should().Contain("IP Address");
        result.Value.Text.Should().Contain("High");
        result.Value.Text.Should().Contain("Domain Name");
        result.Value.Text.Should().Contain("Medium");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyHtml_ShouldReturnEmptyText()
    {
        // Arrange
        var html = "<html><head></head><body></body></html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithMalformedHtml_ShouldStillParse()
    {
        // Arrange
        var html = @"
<html>
<body>
    <p>Unclosed paragraph
    <div>Some content
    <p>Another paragraph</p>
</body>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Unclosed paragraph");
        result.Value.Text.Should().Contain("Some content");
        result.Value.Text.Should().Contain("Another paragraph");
    }

    [Fact]
    public async Task ParseAsync_WithNullStream_ShouldReturnFailure()
    {
        // Act
        var result = await _parser.ParseAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ShouldPreserveWhitespaceCorrectly()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <p>First paragraph</p>
    <p>Second paragraph</p>
</body>
</html>";
        var stream = CreateStream(html);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("First paragraph");
        result.Value.Text.Should().Contain("Second paragraph");
        // Should have proper spacing between elements
        result.Value.Text.Trim().Should().NotBeEmpty();
    }

    private static Stream CreateStream(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }
}
