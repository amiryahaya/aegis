using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.DocumentParsing;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Text;

namespace Aegis.UnitTests.Services.DocumentParsing;

public class SpreadsheetParserTests
{
    private readonly SpreadsheetParser _parser;

    public SpreadsheetParserTests()
    {
        _parser = new SpreadsheetParser(NullLogger<SpreadsheetParser>.Instance);
    }

    [Fact]
    public void SupportedExtensions_ShouldIncludeXlsxAndCsv()
    {
        // Assert
        _parser.SupportedExtensions.Should().Contain(".xlsx");
        _parser.SupportedExtensions.Should().Contain(".csv");
    }

    [Fact]
    public async Task ParseAsync_WithValidXlsx_ShouldExtractText()
    {
        // Arrange
        var xlsxContent = CreateTestXlsx("Sheet1", new[]
        {
            new[] { "Name", "Age", "City" },
            new[] { "John Doe", "30", "New York" },
            new[] { "Jane Smith", "25", "Los Angeles" }
        });

        using var stream = new MemoryStream(xlsxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Name");
        result.Value.Text.Should().Contain("John Doe");
        result.Value.Text.Should().Contain("Jane Smith");
    }

    [Fact]
    public async Task ParseAsync_WithMultipleSheets_ShouldExtractAllSheets()
    {
        // Arrange
        var xlsxContent = CreateTestXlsxWithMultipleSheets(
            ("Sheet1", new[] { new[] { "Data1" } }),
            ("Sheet2", new[] { new[] { "Data2" } }));

        using var stream = new MemoryStream(xlsxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Data1");
        result.Value.Text.Should().Contain("Data2");
    }

    [Fact]
    public async Task ParseAsync_WithValidCsv_ShouldExtractText()
    {
        // Arrange
        var csvContent = CreateTestCsv(new[]
        {
            new[] { "Name", "Age", "City" },
            new[] { "John Doe", "30", "New York" },
            new[] { "Jane Smith", "25", "Los Angeles" }
        });

        using var stream = new MemoryStream(csvContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("Name");
        result.Value.Text.Should().Contain("John Doe");
        result.Value.Text.Should().Contain("Jane Smith");
    }

    [Fact]
    public async Task ParseAsync_WithXlsx_ShouldExtractMetadata()
    {
        // Arrange
        var xlsxContent = CreateTestXlsxWithMetadata(
            "Sheet1",
            new[] { new[] { "Test" } },
            title: "Test Workbook",
            author: "Test Author");

        using var stream = new MemoryStream(xlsxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("Title");
        result.Value.Metadata["Title"].Should().Be("Test Workbook");
        result.Value.Metadata.Should().ContainKey("Author");
        result.Value.Metadata["Author"].Should().Be("Test Author");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyXlsx_ShouldReturnEmptyText()
    {
        // Arrange
        var xlsxContent = CreateTestXlsx("Sheet1", Array.Empty<string[]>());

        using var stream = new MemoryStream(xlsxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithEmptyCsv_ShouldReturnEmptyText()
    {
        // Arrange
        var csvContent = CreateTestCsv(Array.Empty<string[]>());

        using var stream = new MemoryStream(csvContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithInvalidXlsx_ShouldReturnFailure()
    {
        // Arrange
        // ZIP signature (PK) but invalid Excel content
        var invalidContent = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        using var stream = new MemoryStream(invalidContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidFormat");
    }

    [Fact]
    public async Task ParseAsync_WithNullStream_ShouldReturnFailure()
    {
        // Act
        var result = await _parser.ParseAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    private static byte[] CreateTestXlsx(string sheetName, string[][] data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        for (int row = 0; row < data.Length; row++)
        {
            for (int col = 0; col < data[row].Length; col++)
            {
                worksheet.Cell(row + 1, col + 1).Value = data[row][col];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateTestXlsxWithMultipleSheets(params (string sheetName, string[][] data)[] sheets)
    {
        using var workbook = new XLWorkbook();

        foreach (var (sheetName, data) in sheets)
        {
            var worksheet = workbook.Worksheets.Add(sheetName);
            for (int row = 0; row < data.Length; row++)
            {
                for (int col = 0; col < data[row].Length; col++)
                {
                    worksheet.Cell(row + 1, col + 1).Value = data[row][col];
                }
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateTestXlsxWithMetadata(string sheetName, string[][] data, string title, string author)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        workbook.Properties.Title = title;
        workbook.Properties.Author = author;

        for (int row = 0; row < data.Length; row++)
        {
            for (int col = 0; col < data[row].Length; col++)
            {
                worksheet.Cell(row + 1, col + 1).Value = data[row][col];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateTestCsv(string[][] data)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        foreach (var row in data)
        {
            foreach (var cell in row)
            {
                csv.WriteField(cell);
            }
            csv.NextRecord();
        }

        writer.Flush();
        return stream.ToArray();
    }
}
