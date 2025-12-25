using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Tables;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.Tables;

public class HtmlTableExtractorTests
{
    private readonly HtmlTableExtractor _extractor;

    public HtmlTableExtractorTests()
    {
        _extractor = new HtmlTableExtractor(NullLogger<HtmlTableExtractor>.Instance);
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithSimpleTable_ShouldExtractCorrectly()
    {
        // Arrange
        var html = @"
<table>
    <thead>
        <tr>
            <th>Name</th>
            <th>Age</th>
            <th>City</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>John Doe</td>
            <td>30</td>
            <td>New York</td>
        </tr>
        <tr>
            <td>Jane Smith</td>
            <td>25</td>
            <td>Los Angeles</td>
        </tr>
    </tbody>
</table>";

        // Act
        var result = await _extractor.ExtractFromHtmlAsync(html);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var table = result.Value[0];
        table.Headers.Should().HaveCount(3);
        table.Headers.Should().Contain(new[] { "Name", "Age", "City" });
        table.Rows.Should().HaveCount(2);
        table.Rows[0].Should().Contain(new[] { "John Doe", "30", "New York" });
        table.Rows[1].Should().Contain(new[] { "Jane Smith", "25", "Los Angeles" });
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithMultipleTables_ShouldExtractAll()
    {
        // Arrange
        var html = @"
<div>
    <table>
        <tr><th>Column1</th></tr>
        <tr><td>Value1</td></tr>
    </table>
    <p>Some text between tables</p>
    <table>
        <tr><th>Column2</th></tr>
        <tr><td>Value2</td></tr>
    </table>
</div>";

        // Act
        var result = await _extractor.ExtractFromHtmlAsync(html);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Headers.Should().Contain("Column1");
        result.Value[1].Headers.Should().Contain("Column2");
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithTableWithoutThead_ShouldUseFirstRowAsHeaders()
    {
        // Arrange
        var html = @"
<table>
    <tr>
        <td><strong>Name</strong></td>
        <td><strong>Value</strong></td>
    </tr>
    <tr>
        <td>Item 1</td>
        <td>100</td>
    </tr>
</table>";

        // Act
        var result = await _extractor.ExtractFromHtmlAsync(html);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var table = result.Value[0];
        table.Headers.Should().HaveCount(2);
        table.Headers[0].Should().Contain("Name");
        table.Headers[1].Should().Contain("Value");
        table.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithNoTables_ShouldReturnEmptyList()
    {
        // Arrange
        var html = @"
<div>
    <h1>No tables here</h1>
    <p>Just some paragraphs</p>
</div>";

        // Act
        var result = await _extractor.ExtractFromHtmlAsync(html);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithEmptyTable_ShouldReturnEmptyTable()
    {
        // Arrange
        var html = "<table></table>";

        // Act
        var result = await _extractor.ExtractFromHtmlAsync(html);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty(); // Empty tables are skipped
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithNullHtml_ShouldReturnFailure()
    {
        // Act
        var result = await _extractor.ExtractFromHtmlAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ToMarkdown_ShouldFormatCorrectly()
    {
        // Arrange
        var table = new ExtractedTable
        {
            Headers = new[] { "Name", "Age" },
            Rows = new[]
            {
                new[] { "John", "30" }.AsEnumerable().ToList().AsReadOnly(),
                new[] { "Jane", "25" }.AsEnumerable().ToList().AsReadOnly()
            }
        };

        // Act
        var markdown = table.ToMarkdown();

        // Assert
        markdown.Should().Contain("| Name | Age |");
        markdown.Should().Contain("|---|---|");
        markdown.Should().Contain("| John | 30 |");
        markdown.Should().Contain("| Jane | 25 |");
    }

    [Fact]
    public async Task ToPlainText_ShouldFormatCorrectly()
    {
        // Arrange
        var table = new ExtractedTable
        {
            Headers = new[] { "Name", "Age" },
            Rows = new[]
            {
                new[] { "John", "30" }.AsEnumerable().ToList().AsReadOnly(),
                new[] { "Jane", "25" }.AsEnumerable().ToList().AsReadOnly()
            }
        };

        // Act
        var plainText = table.ToPlainText();

        // Assert
        plainText.Should().Contain("Name\tAge");
        plainText.Should().Contain("John\t30");
        plainText.Should().Contain("Jane\t25");
    }

    [Fact]
    public async Task ExtractFromHtmlAsync_WithNestedTables_ShouldExtractOuterTable()
    {
        // Arrange
        var html = @"
<table>
    <tr>
        <th>Header1</th>
        <th>Header2</th>
    </tr>
    <tr>
        <td>Data1</td>
        <td>
            <table>
                <tr><td>Nested</td></tr>
            </table>
        </td>
    </tr>
</table>";

        // Act
        var result = await _extractor.ExtractFromHtmlAsync(html);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2); // Both outer and nested table
    }
}
