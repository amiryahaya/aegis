using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for exporting data to various formats (JSON, CSV, PDF, DOCX)
/// </summary>
public interface IDataExporter
{
    /// <summary>
    /// Export data to JSON format
    /// </summary>
    Task<Result<ExportResult>> ExportToJsonAsync(
        DataExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export data to CSV format
    /// </summary>
    Task<Result<ExportResult>> ExportToCsvAsync(
        DataExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export data to PDF format
    /// </summary>
    Task<Result<ExportResult>> ExportToPdfAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export data to DOCX format
    /// </summary>
    Task<Result<ExportResult>> ExportToDocxAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export data to Excel format
    /// </summary>
    Task<Result<ExportResult>> ExportToExcelAsync(
        DataExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get supported export formats
    /// </summary>
    Task<Result<List<ExportFormatInfo>>> GetSupportedFormatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a report from a template
    /// </summary>
    Task<Result<ExportResult>> GenerateReportAsync(
        ReportGenerationRequest request,
        CancellationToken cancellationToken = default);
}

#region Request/Response Records

/// <summary>
/// Request for data export (JSON, CSV, Excel)
/// </summary>
public record DataExportRequest
{
    /// <summary>
    /// Type of data to export
    /// </summary>
    public required DataExportType DataType { get; init; }

    /// <summary>
    /// The data to export (list of objects or dictionary)
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Columns to include (null = all)
    /// </summary>
    public List<ColumnDefinition>? Columns { get; init; }

    /// <summary>
    /// Filters to apply
    /// </summary>
    public Dictionary<string, object>? Filters { get; init; }

    /// <summary>
    /// Optional workspace scope
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Date range start
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// Date range end
    /// </summary>
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Include headers in output
    /// </summary>
    public bool IncludeHeaders { get; init; } = true;

    /// <summary>
    /// File name for the export
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Encoding for text-based formats
    /// </summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary>
    /// Pretty print JSON
    /// </summary>
    public bool PrettyPrint { get; init; } = true;

    /// <summary>
    /// CSV delimiter
    /// </summary>
    public string CsvDelimiter { get; init; } = ",";
}

/// <summary>
/// Column definition for export
/// </summary>
public record ColumnDefinition
{
    public required string Field { get; init; }
    public string? Header { get; init; }
    public string? Format { get; init; }
    public int? Width { get; init; }
    public ColumnAlignment Alignment { get; init; } = ColumnAlignment.Left;
}

/// <summary>
/// Request for report export (PDF, DOCX)
/// </summary>
public record ReportExportRequest
{
    /// <summary>
    /// Report title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Report subtitle or description
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// Sections of the report
    /// </summary>
    public required List<ReportSection> Sections { get; init; }

    /// <summary>
    /// Header content
    /// </summary>
    public string? HeaderText { get; init; }

    /// <summary>
    /// Footer content
    /// </summary>
    public string? FooterText { get; init; }

    /// <summary>
    /// Author name
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Include page numbers
    /// </summary>
    public bool IncludePageNumbers { get; init; } = true;

    /// <summary>
    /// Include table of contents
    /// </summary>
    public bool IncludeTableOfContents { get; init; } = false;

    /// <summary>
    /// Page size
    /// </summary>
    public PageSize PageSize { get; init; } = PageSize.A4;

    /// <summary>
    /// Page orientation
    /// </summary>
    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;

    /// <summary>
    /// File name for the export
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Custom styles
    /// </summary>
    public ReportStyles? Styles { get; init; }
}

/// <summary>
/// A section in a report
/// </summary>
public record ReportSection
{
    public required string Title { get; init; }
    public ReportSectionType Type { get; init; } = ReportSectionType.Text;
    public string? Content { get; init; }
    public object? Data { get; init; }
    public List<ColumnDefinition>? Columns { get; init; }
    public ChartOptions? ChartOptions { get; init; }
    public int HeadingLevel { get; init; } = 2;
}

/// <summary>
/// Chart options for report sections
/// </summary>
public record ChartOptions
{
    public ChartType Type { get; init; } = ChartType.Bar;
    public string? LabelField { get; init; }
    public string? ValueField { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public bool ShowLegend { get; init; } = true;
}

/// <summary>
/// Report styling options
/// </summary>
public record ReportStyles
{
    public string? FontFamily { get; init; }
    public int BaseFontSize { get; init; } = 12;
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public int MarginTop { get; init; } = 72;
    public int MarginBottom { get; init; } = 72;
    public int MarginLeft { get; init; } = 72;
    public int MarginRight { get; init; } = 72;
}

/// <summary>
/// Request for report generation from template
/// </summary>
public record ReportGenerationRequest
{
    /// <summary>
    /// Template type or ID
    /// </summary>
    public required ReportTemplate Template { get; init; }

    /// <summary>
    /// Data to populate the template
    /// </summary>
    public required Dictionary<string, object> Data { get; init; }

    /// <summary>
    /// Output format
    /// </summary>
    public ExportFormat OutputFormat { get; init; } = ExportFormat.Pdf;

    /// <summary>
    /// Optional title override
    /// </summary>
    public string? TitleOverride { get; init; }

    /// <summary>
    /// File name for the export
    /// </summary>
    public string? FileName { get; init; }
}

/// <summary>
/// Result of an export operation
/// </summary>
public record ExportResult
{
    /// <summary>
    /// The exported data as bytes
    /// </summary>
    public required byte[] Content { get; init; }

    /// <summary>
    /// MIME type of the content
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Suggested file name
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Size in bytes
    /// </summary>
    public long SizeBytes => Content.Length;

    /// <summary>
    /// Number of records exported
    /// </summary>
    public int RecordCount { get; init; }

    /// <summary>
    /// Export generation time
    /// </summary>
    public TimeSpan GenerationTime { get; init; }

    /// <summary>
    /// Export timestamp
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Information about a supported export format
/// </summary>
public record ExportFormatInfo
{
    public required ExportFormat Format { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public bool SupportsCharts { get; init; }
    public bool SupportsImages { get; init; }
    public bool SupportsTables { get; init; }
    public bool SupportsFormatting { get; init; }
}

#endregion

#region Enums

/// <summary>
/// Types of data that can be exported
/// </summary>
public enum DataExportType
{
    QueryHistory,
    Documents,
    Users,
    Teams,
    Workspaces,
    AuditLogs,
    UsageAnalytics,
    Conversations,
    Custom
}

/// <summary>
/// Column alignment options
/// </summary>
public enum ColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Types of report sections
/// </summary>
public enum ReportSectionType
{
    Text,
    Table,
    Chart,
    Image,
    PageBreak,
    Summary
}

/// <summary>
/// Chart types for reports
/// </summary>
public enum ChartType
{
    Bar,
    Line,
    Pie,
    Doughnut,
    Area
}

/// <summary>
/// Page sizes for reports
/// </summary>
public enum PageSize
{
    A4,
    Letter,
    Legal,
    A3
}

/// <summary>
/// Page orientation for reports
/// </summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

/// <summary>
/// Pre-defined report templates
/// </summary>
public enum ReportTemplate
{
    UsageSummary,
    AuditReport,
    WorkspaceAnalytics,
    CostReport,
    SecurityReport,
    QueryAnalysis,
    Custom
}

#endregion

#region Errors

public static class DataExporterErrors
{
    public static readonly Error InvalidData = Error.Validation(
        "DataExporter.InvalidData", "Invalid or empty data for export");

    public static readonly Error UnsupportedFormat = Error.Validation(
        "DataExporter.UnsupportedFormat", "Export format is not supported");

    public static readonly Error ExportFailed = Error.Internal(
        "DataExporter.ExportFailed", "Failed to export data");

    public static readonly Error TemplateNotFound = Error.NotFound(
        "DataExporter.TemplateNotFound", "Report template not found");

    public static readonly Error InvalidTemplate = Error.Validation(
        "DataExporter.InvalidTemplate", "Invalid report template");
}

#endregion
