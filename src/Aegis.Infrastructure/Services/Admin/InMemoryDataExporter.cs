using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Admin;

/// <summary>
/// In-memory implementation of data exporter for development and testing
/// </summary>
public class InMemoryDataExporter : IDataExporter
{
    private readonly ILogger<InMemoryDataExporter> _logger;

    public InMemoryDataExporter(ILogger<InMemoryDataExporter> logger)
    {
        _logger = logger;
    }

    public Task<Result<ExportResult>> ExportToJsonAsync(
        DataExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (request.Data == null)
        {
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.InvalidData));
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = request.PrettyPrint,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(request.Data, options);
            var recordCount = GetRecordCount(request.Data);

            sw.Stop();

            var result = new ExportResult
            {
                Content = jsonBytes,
                ContentType = "application/json",
                FileName = request.FileName ?? $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                RecordCount = recordCount,
                GenerationTime = sw.Elapsed,
                GeneratedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Exported {RecordCount} records to JSON", recordCount);

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to JSON");
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.ExportFailed));
        }
    }

    public Task<Result<ExportResult>> ExportToCsvAsync(
        DataExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (request.Data == null)
        {
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.InvalidData));
        }

        try
        {
            var sb = new StringBuilder();
            var delimiter = request.CsvDelimiter;
            var records = ConvertToRecords(request.Data);

            if (!records.Any())
            {
                sw.Stop();
                return Task.FromResult(Result.Success(new ExportResult
                {
                    Content = Array.Empty<byte>(),
                    ContentType = "text/csv",
                    FileName = request.FileName ?? $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                    RecordCount = 0,
                    GenerationTime = sw.Elapsed
                }));
            }

            var headers = records.First().Keys.ToList();

            // Headers
            if (request.IncludeHeaders)
            {
                sb.AppendLine(string.Join(delimiter, headers.Select(h => EscapeCsvValue(h, delimiter))));
            }

            // Data rows
            foreach (var record in records)
            {
                var values = headers.Select(h =>
                    record.TryGetValue(h, out var value)
                        ? EscapeCsvValue(value?.ToString() ?? "", delimiter)
                        : "");
                sb.AppendLine(string.Join(delimiter, values));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            sw.Stop();

            var result = new ExportResult
            {
                Content = bytes,
                ContentType = "text/csv",
                FileName = request.FileName ?? $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                RecordCount = records.Count,
                GenerationTime = sw.Elapsed,
                GeneratedAt = DateTime.UtcNow
            };

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to CSV");
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.ExportFailed));
        }
    }

    public Task<Result<ExportResult>> ExportToPdfAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (request.Sections == null || !request.Sections.Any())
        {
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.InvalidData));
        }

        try
        {
            // For in-memory testing, we generate a simplified PDF-like structure
            // In production, this would use a library like QuestPDF, iTextSharp, or similar
            var pdfContent = GenerateSimplePdfContent(request);
            var bytes = Encoding.UTF8.GetBytes(pdfContent);

            var recordCount = request.Sections
                .Where(s => s.Type == ReportSectionType.Table && s.Data != null)
                .Sum(s => GetRecordCount(s.Data!));

            sw.Stop();

            var result = new ExportResult
            {
                Content = bytes,
                ContentType = "application/pdf",
                FileName = request.FileName ?? $"report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf",
                RecordCount = recordCount,
                GenerationTime = sw.Elapsed,
                GeneratedAt = DateTime.UtcNow
            };

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to PDF");
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.ExportFailed));
        }
    }

    public Task<Result<ExportResult>> ExportToDocxAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (request.Sections == null || !request.Sections.Any())
        {
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.InvalidData));
        }

        try
        {
            // For in-memory testing, we generate a simplified DOCX-like structure
            // In production, this would use a library like DocX, OpenXML SDK, or similar
            var docxContent = GenerateSimpleDocxContent(request);
            var bytes = Encoding.UTF8.GetBytes(docxContent);

            sw.Stop();

            var result = new ExportResult
            {
                Content = bytes,
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileName = request.FileName ?? $"document_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx",
                RecordCount = request.Sections.Count,
                GenerationTime = sw.Elapsed,
                GeneratedAt = DateTime.UtcNow
            };

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to DOCX");
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.ExportFailed));
        }
    }

    public Task<Result<ExportResult>> ExportToExcelAsync(
        DataExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (request.Data == null)
        {
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.InvalidData));
        }

        try
        {
            // For in-memory testing, we generate a simplified Excel-like structure
            // In production, this would use a library like ClosedXML, EPPlus, or similar
            var records = ConvertToRecords(request.Data);
            var excelContent = GenerateSimpleExcelContent(records, request.Columns, request.IncludeHeaders);
            var bytes = Encoding.UTF8.GetBytes(excelContent);

            sw.Stop();

            var result = new ExportResult
            {
                Content = bytes,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = request.FileName ?? $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx",
                RecordCount = records.Count,
                GenerationTime = sw.Elapsed,
                GeneratedAt = DateTime.UtcNow
            };

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to Excel");
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.ExportFailed));
        }
    }

    public Task<Result<List<ExportFormatInfo>>> GetSupportedFormatsAsync(
        CancellationToken cancellationToken = default)
    {
        var formats = new List<ExportFormatInfo>
        {
            new ExportFormatInfo
            {
                Format = ExportFormat.Json,
                Name = "JSON",
                Extension = ".json",
                ContentType = "application/json",
                SupportsCharts = false,
                SupportsImages = false,
                SupportsTables = true,
                SupportsFormatting = false
            },
            new ExportFormatInfo
            {
                Format = ExportFormat.Csv,
                Name = "CSV",
                Extension = ".csv",
                ContentType = "text/csv",
                SupportsCharts = false,
                SupportsImages = false,
                SupportsTables = true,
                SupportsFormatting = false
            },
            new ExportFormatInfo
            {
                Format = ExportFormat.Pdf,
                Name = "PDF",
                Extension = ".pdf",
                ContentType = "application/pdf",
                SupportsCharts = true,
                SupportsImages = true,
                SupportsTables = true,
                SupportsFormatting = true
            },
            new ExportFormatInfo
            {
                Format = ExportFormat.Excel,
                Name = "Excel",
                Extension = ".xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                SupportsCharts = true,
                SupportsImages = true,
                SupportsTables = true,
                SupportsFormatting = true
            }
        };

        return Task.FromResult(Result.Success(formats));
    }

    public Task<Result<ExportResult>> GenerateReportAsync(
        ReportGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reportRequest = CreateReportFromTemplate(request);

            return request.OutputFormat switch
            {
                ExportFormat.Pdf => ExportToPdfAsync(reportRequest, cancellationToken),
                ExportFormat.Json => GenerateJsonReportAsync(request, cancellationToken),
                _ => ExportToPdfAsync(reportRequest, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report from template");
            return Task.FromResult(Result.Failure<ExportResult>(DataExporterErrors.ExportFailed));
        }
    }

    #region Private Methods

    private static int GetRecordCount(object data)
    {
        if (data is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Count();
        }
        return 1;
    }

    private static List<Dictionary<string, object>> ConvertToRecords(object data)
    {
        var result = new List<Dictionary<string, object>>();

        if (data is IEnumerable<Dictionary<string, object>> dicts)
        {
            return dicts.ToList();
        }

        if (data is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is Dictionary<string, object> dict)
                {
                    result.Add(dict);
                }
                else
                {
                    var record = new Dictionary<string, object>();
                    foreach (var prop in item.GetType().GetProperties())
                    {
                        record[prop.Name] = prop.GetValue(item) ?? "";
                    }
                    result.Add(record);
                }
            }
        }

        return result;
    }

    private static string EscapeCsvValue(string value, string delimiter)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string GenerateSimplePdfContent(ReportExportRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("%PDF-1.4");
        sb.AppendLine($"% Title: {request.Title}");
        if (!string.IsNullOrEmpty(request.Author))
        {
            sb.AppendLine($"% Author: {request.Author}");
        }
        sb.AppendLine();

        foreach (var section in request.Sections)
        {
            sb.AppendLine($"## {section.Title}");
            if (!string.IsNullOrEmpty(section.Content))
            {
                sb.AppendLine(section.Content);
            }
            if (section.Data != null && section.Type == ReportSectionType.Table)
            {
                var records = ConvertToRecords(section.Data);
                foreach (var record in records.Take(10))
                {
                    sb.AppendLine(string.Join(" | ", record.Values));
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateSimpleDocxContent(ReportExportRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<document>");
        sb.AppendLine($"  <title>{request.Title}</title>");

        foreach (var section in request.Sections)
        {
            sb.AppendLine($"  <section heading=\"{section.HeadingLevel}\">");
            sb.AppendLine($"    <title>{section.Title}</title>");
            if (!string.IsNullOrEmpty(section.Content))
            {
                sb.AppendLine($"    <content>{section.Content}</content>");
            }
            sb.AppendLine("  </section>");
        }

        sb.AppendLine("</document>");
        return sb.ToString();
    }

    private static string GenerateSimpleExcelContent(
        List<Dictionary<string, object>> records,
        List<ColumnDefinition>? columns,
        bool includeHeaders)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<worksheet>");

        if (records.Any())
        {
            var headers = columns?.Select(c => c.Header ?? c.Field).ToList()
                ?? records.First().Keys.ToList();

            if (includeHeaders)
            {
                sb.AppendLine($"  <row type=\"header\">{string.Join(",", headers)}</row>");
            }

            var fields = columns?.Select(c => c.Field).ToList() ?? records.First().Keys.ToList();
            foreach (var record in records)
            {
                var values = fields.Select(f => record.TryGetValue(f, out var v) ? v?.ToString() ?? "" : "");
                sb.AppendLine($"  <row>{string.Join(",", values)}</row>");
            }
        }

        sb.AppendLine("</worksheet>");
        return sb.ToString();
    }

    private static ReportExportRequest CreateReportFromTemplate(ReportGenerationRequest request)
    {
        var title = request.TitleOverride ?? GetTemplateTitle(request.Template);
        var sections = new List<ReportSection>();

        switch (request.Template)
        {
            case ReportTemplate.UsageSummary:
                sections.Add(new ReportSection
                {
                    Title = "Usage Summary",
                    Type = ReportSectionType.Summary,
                    Content = FormatTemplateData(request.Data)
                });
                break;

            case ReportTemplate.AuditReport:
                sections.Add(new ReportSection
                {
                    Title = "Audit Report",
                    Type = ReportSectionType.Text,
                    Content = FormatTemplateData(request.Data)
                });
                break;

            default:
                sections.Add(new ReportSection
                {
                    Title = "Report Data",
                    Type = ReportSectionType.Text,
                    Content = FormatTemplateData(request.Data)
                });
                break;
        }

        return new ReportExportRequest
        {
            Title = title,
            Sections = sections,
            FileName = request.FileName
        };
    }

    private static string GetTemplateTitle(ReportTemplate template)
    {
        return template switch
        {
            ReportTemplate.UsageSummary => "Usage Summary Report",
            ReportTemplate.AuditReport => "Audit Report",
            ReportTemplate.WorkspaceAnalytics => "Workspace Analytics Report",
            ReportTemplate.CostReport => "Cost Analysis Report",
            ReportTemplate.SecurityReport => "Security Report",
            ReportTemplate.QueryAnalysis => "Query Analysis Report",
            _ => "Report"
        };
    }

    private static string FormatTemplateData(Dictionary<string, object> data)
    {
        var sb = new StringBuilder();
        foreach (var kvp in data)
        {
            sb.AppendLine($"{kvp.Key}: {kvp.Value}");
        }
        return sb.ToString();
    }

    private Task<Result<ExportResult>> GenerateJsonReportAsync(
        ReportGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var reportData = new
        {
            Title = request.TitleOverride ?? GetTemplateTitle(request.Template),
            Template = request.Template.ToString(),
            GeneratedAt = DateTime.UtcNow,
            Data = request.Data
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(reportData, options);
        sw.Stop();

        var result = new ExportResult
        {
            Content = bytes,
            ContentType = "application/json",
            FileName = request.FileName ?? $"report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
            RecordCount = request.Data.Count,
            GenerationTime = sw.Elapsed,
            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(Result.Success(result));
    }

    #endregion
}
