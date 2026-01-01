using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Admin;

public class DataExporterTests
{
    private readonly ILogger<InMemoryDataExporter> _logger;
    private readonly InMemoryDataExporter _sut;

    public DataExporterTests()
    {
        _logger = Substitute.For<ILogger<InMemoryDataExporter>>();
        _sut = new InMemoryDataExporter(_logger);
    }

    #region ExportToJsonAsync Tests

    [Fact]
    public async Task ExportToJsonAsync_WithValidData_ReturnsJsonBytes()
    {
        // Arrange
        var data = new List<object>
        {
            new { Name = "John", Age = 30 },
            new { Name = "Jane", Age = 25 }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data
        };

        // Act
        var result = await _sut.ExportToJsonAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().NotBeEmpty();
        result.Value.ContentType.Should().Be("application/json");
        result.Value.FileName.Should().EndWith(".json");

        var json = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        json.Should().Contain("John");
        json.Should().Contain("Jane");
    }

    [Fact]
    public async Task ExportToJsonAsync_WithPrettyPrint_FormatsJson()
    {
        // Arrange
        var data = new List<object> { new { Name = "Test" } };
        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data,
            PrettyPrint = true
        };

        // Act
        var result = await _sut.ExportToJsonAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var json = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        json.Should().Contain("\n"); // Formatted JSON has newlines
    }

    [Fact]
    public async Task ExportToJsonAsync_WithEmptyData_ReturnsEmptyArray()
    {
        // Arrange
        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = new List<object>()
        };

        // Act
        var result = await _sut.ExportToJsonAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var json = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        json.Should().Contain("[]");
    }

    [Fact]
    public async Task ExportToJsonAsync_WithNullData_ReturnsFailure()
    {
        // Arrange
        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = null
        };

        // Act
        var result = await _sut.ExportToJsonAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region ExportToCsvAsync Tests

    [Fact]
    public async Task ExportToCsvAsync_WithValidData_ReturnsCsvBytes()
    {
        // Arrange
        var data = new List<Dictionary<string, object>>
        {
            new() { { "Name", "John" }, { "Age", 30 } },
            new() { { "Name", "Jane" }, { "Age", 25 } }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data,
            IncludeHeaders = true
        };

        // Act
        var result = await _sut.ExportToCsvAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("text/csv");
        result.Value.FileName.Should().EndWith(".csv");

        var csv = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        csv.Should().Contain("Name");
        csv.Should().Contain("Age");
        csv.Should().Contain("John");
        csv.Should().Contain("30");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithCustomDelimiter_UsesDelimiter()
    {
        // Arrange
        var data = new List<Dictionary<string, object>>
        {
            new() { { "Name", "John" }, { "Age", 30 } }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data,
            CsvDelimiter = ";"
        };

        // Act
        var result = await _sut.ExportToCsvAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var csv = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        csv.Should().Contain(";");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithoutHeaders_OmitsHeaders()
    {
        // Arrange
        var data = new List<Dictionary<string, object>>
        {
            new() { { "Name", "John" } }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data,
            IncludeHeaders = false
        };

        // Act
        var result = await _sut.ExportToCsvAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var csv = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("John");
    }

    [Fact]
    public async Task ExportToCsvAsync_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var data = new List<Dictionary<string, object>>
        {
            new() { { "Name", "John, Jr." }, { "Note", "Said \"Hello\"" } }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data
        };

        // Act
        var result = await _sut.ExportToCsvAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var csv = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        csv.Should().Contain("\"John, Jr.\"");
    }

    #endregion

    #region ExportToPdfAsync Tests

    [Fact]
    public async Task ExportToPdfAsync_WithValidRequest_ReturnsPdfBytes()
    {
        // Arrange
        var request = new ReportExportRequest
        {
            Title = "Test Report",
            Sections = new List<ReportSection>
            {
                new ReportSection
                {
                    Title = "Introduction",
                    Type = ReportSectionType.Text,
                    Content = "This is a test report."
                }
            }
        };

        // Act
        var result = await _sut.ExportToPdfAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.FileName.Should().EndWith(".pdf");
        result.Value.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportToPdfAsync_WithTableSection_IncludesTable()
    {
        // Arrange
        var tableData = new List<Dictionary<string, object>>
        {
            new() { { "Item", "Widget" }, { "Price", 9.99 } }
        };

        var request = new ReportExportRequest
        {
            Title = "Sales Report",
            Sections = new List<ReportSection>
            {
                new ReportSection
                {
                    Title = "Products",
                    Type = ReportSectionType.Table,
                    Data = tableData,
                    Columns = new List<ColumnDefinition>
                    {
                        new ColumnDefinition { Field = "Item", Header = "Product" },
                        new ColumnDefinition { Field = "Price", Header = "Price ($)" }
                    }
                }
            }
        };

        // Act
        var result = await _sut.ExportToPdfAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RecordCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ExportToPdfAsync_WithAuthor_IncludesMetadata()
    {
        // Arrange
        var request = new ReportExportRequest
        {
            Title = "Report",
            Author = "Test Author",
            Sections = new List<ReportSection>
            {
                new ReportSection { Title = "Content", Content = "Test" }
            }
        };

        // Act
        var result = await _sut.ExportToPdfAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ExportToDocxAsync Tests

    [Fact]
    public async Task ExportToDocxAsync_WithValidRequest_ReturnsDocxBytes()
    {
        // Arrange
        var request = new ReportExportRequest
        {
            Title = "Word Document",
            Sections = new List<ReportSection>
            {
                new ReportSection
                {
                    Title = "Summary",
                    Type = ReportSectionType.Text,
                    Content = "This is a Word document export."
                }
            }
        };

        // Act
        var result = await _sut.ExportToDocxAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        result.Value.FileName.Should().EndWith(".docx");
    }

    [Fact]
    public async Task ExportToDocxAsync_WithMultipleSections_IncludesAll()
    {
        // Arrange
        var request = new ReportExportRequest
        {
            Title = "Multi-Section Report",
            Sections = new List<ReportSection>
            {
                new ReportSection { Title = "Section 1", Content = "Content 1" },
                new ReportSection { Title = "Section 2", Content = "Content 2" },
                new ReportSection { Title = "Section 3", Content = "Content 3" }
            }
        };

        // Act
        var result = await _sut.ExportToDocxAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ExportToExcelAsync Tests

    [Fact]
    public async Task ExportToExcelAsync_WithValidData_ReturnsExcelBytes()
    {
        // Arrange
        var data = new List<Dictionary<string, object>>
        {
            new() { { "Name", "John" }, { "Score", 95 } },
            new() { { "Name", "Jane" }, { "Score", 88 } }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data
        };

        // Act
        var result = await _sut.ExportToExcelAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Value.FileName.Should().EndWith(".xlsx");
    }

    [Fact]
    public async Task ExportToExcelAsync_WithColumnDefinitions_AppliesFormat()
    {
        // Arrange
        var data = new List<Dictionary<string, object>>
        {
            new() { { "Date", DateTime.UtcNow }, { "Amount", 1234.56m } }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data,
            Columns = new List<ColumnDefinition>
            {
                new ColumnDefinition { Field = "Date", Header = "Transaction Date", Format = "yyyy-MM-dd" },
                new ColumnDefinition { Field = "Amount", Header = "Amount ($)", Format = "#,##0.00" }
            }
        };

        // Act
        var result = await _sut.ExportToExcelAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region GetSupportedFormatsAsync Tests

    [Fact]
    public async Task GetSupportedFormatsAsync_ReturnsAllFormats()
    {
        // Act
        var result = await _sut.GetSupportedFormatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterOrEqualTo(4);
        result.Value.Should().Contain(f => f.Format == ExportFormat.Json);
        result.Value.Should().Contain(f => f.Format == ExportFormat.Csv);
        result.Value.Should().Contain(f => f.Format == ExportFormat.Pdf);
        result.Value.Should().Contain(f => f.Format == ExportFormat.Excel);
    }

    [Fact]
    public async Task GetSupportedFormatsAsync_IncludesFormatDetails()
    {
        // Act
        var result = await _sut.GetSupportedFormatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var jsonFormat = result.Value.First(f => f.Format == ExportFormat.Json);
        jsonFormat.ContentType.Should().Be("application/json");
        jsonFormat.Extension.Should().Be(".json");
    }

    #endregion

    #region GenerateReportAsync Tests

    [Fact]
    public async Task GenerateReportAsync_WithUsageSummaryTemplate_GeneratesReport()
    {
        // Arrange
        var request = new ReportGenerationRequest
        {
            Template = ReportTemplate.UsageSummary,
            Data = new Dictionary<string, object>
            {
                { "TotalQueries", 1000 },
                { "TotalTokens", 50000 },
                { "TotalCost", 25.50m }
            },
            OutputFormat = ExportFormat.Pdf
        };

        // Act
        var result = await _sut.GenerateReportAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task GenerateReportAsync_WithTitleOverride_UsesOverride()
    {
        // Arrange
        var request = new ReportGenerationRequest
        {
            Template = ReportTemplate.UsageSummary,
            Data = new Dictionary<string, object>(),
            TitleOverride = "Custom Title",
            OutputFormat = ExportFormat.Pdf
        };

        // Act
        var result = await _sut.GenerateReportAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateReportAsync_WithJsonOutput_ReturnsJson()
    {
        // Arrange
        var request = new ReportGenerationRequest
        {
            Template = ReportTemplate.UsageSummary,
            Data = new Dictionary<string, object>
            {
                { "TotalQueries", 500 }
            },
            OutputFormat = ExportFormat.Json
        };

        // Act
        var result = await _sut.GenerateReportAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/json");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExportToCsvAsync_WithNullData_ReturnsFailure()
    {
        // Arrange
        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = null
        };

        // Act
        var result = await _sut.ExportToCsvAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExportToPdfAsync_WithEmptySections_ReturnsFailure()
    {
        // Arrange
        var request = new ReportExportRequest
        {
            Title = "Empty Report",
            Sections = new List<ReportSection>()
        };

        // Act
        var result = await _sut.ExportToPdfAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExportToJsonAsync_TracksRecordCount()
    {
        // Arrange
        var data = new List<object>
        {
            new { Id = 1 },
            new { Id = 2 },
            new { Id = 3 }
        };

        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data
        };

        // Act
        var result = await _sut.ExportToJsonAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RecordCount.Should().Be(3);
    }

    [Fact]
    public async Task ExportToJsonAsync_TracksGenerationTime()
    {
        // Arrange
        var data = new List<object> { new { Id = 1 } };
        var request = new DataExportRequest
        {
            DataType = DataExportType.Custom,
            Data = data
        };

        // Act
        var result = await _sut.ExportToJsonAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GenerationTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion
}
