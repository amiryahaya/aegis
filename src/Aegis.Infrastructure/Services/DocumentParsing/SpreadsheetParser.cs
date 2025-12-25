using Aegis.Domain.Common;
using Aegis.Domain.Services;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace Aegis.Infrastructure.Services.DocumentParsing;

public class SpreadsheetParser : IDocumentParser
{
    private readonly ILogger<SpreadsheetParser> _logger;

    public SpreadsheetParser(ILogger<SpreadsheetParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => new[] { ".xlsx", ".csv" };

    public async Task<Result<ParsedDocument>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.NullStream", "Stream cannot be null"));
        }

        try
        {
            // Try to detect if it's CSV or Excel by checking the stream
            var buffer = new byte[4];
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            stream.Position = 0; // Reset stream position

            // Check if it's a ZIP file (Excel files are ZIP archives)
            // ZIP files start with "PK" (0x50 0x4B)
            var isZip = bytesRead >= 2 && buffer[0] == 0x50 && buffer[1] == 0x4B;

            if (isZip)
            {
                return await ParseExcelAsync(stream, cancellationToken);
            }
            else
            {
                return await ParseCsvAsync(stream, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse spreadsheet document");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "Failed to parse spreadsheet document"));
        }
    }

    private async Task<Result<ParsedDocument>> ParseExcelAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(stream);

                var textBuilder = new StringBuilder();

                foreach (var worksheet in workbook.Worksheets)
                {
                    // Extract all cells with values
                    var usedRange = worksheet.RangeUsed();
                    if (usedRange != null)
                    {
                        var sheetContent = new StringBuilder();

                        foreach (var row in usedRange.Rows())
                        {
                            var rowValues = new List<string>();
                            foreach (var cell in row.Cells())
                            {
                                var value = cell.GetString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    rowValues.Add(value);
                                }
                            }

                            if (rowValues.Count > 0)
                            {
                                sheetContent.AppendLine(string.Join("\t", rowValues));
                            }
                        }

                        // Only add sheet header and content if there's actual data
                        if (sheetContent.Length > 0)
                        {
                            if (!string.IsNullOrWhiteSpace(worksheet.Name))
                            {
                                textBuilder.AppendLine($"=== {worksheet.Name} ===");
                            }
                            textBuilder.Append(sheetContent);
                            textBuilder.AppendLine(); // Blank line between sheets
                        }
                    }
                }

                var metadata = ExtractExcelMetadata(workbook);

                return Result<ParsedDocument>.Success(new ParsedDocument
                {
                    Text = textBuilder.ToString().Trim(),
                    Metadata = metadata
                });
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException ||
                                    ex is System.IO.IOException ||
                                    ex.GetType().Name.Contains("Zip") ||
                                    ex.GetType().Name.Contains("Format"))
        {
            _logger.LogError(ex, "Failed to parse Excel - invalid file format");
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.InvalidFormat", "The file is not a valid Excel document"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Excel document");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "Failed to parse Excel document"));
        }
    }

    private async Task<Result<ParsedDocument>> ParseCsvAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false, // Treat all rows as data
                    BadDataFound = null // Ignore bad data
                });

                var textBuilder = new StringBuilder();

                while (csv.Read())
                {
                    var rowValues = new List<string>();
                    for (int i = 0; csv.TryGetField<string>(i, out var value); i++)
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            rowValues.Add(value);
                        }
                    }

                    if (rowValues.Count > 0)
                    {
                        textBuilder.AppendLine(string.Join("\t", rowValues));
                    }
                }

                return Result<ParsedDocument>.Success(new ParsedDocument
                {
                    Text = textBuilder.ToString().Trim(),
                    Metadata = new Dictionary<string, string>
                    {
                        ["Format"] = "CSV"
                    }
                });
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV document");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "Failed to parse CSV document"));
        }
    }

    private static Dictionary<string, string> ExtractExcelMetadata(XLWorkbook workbook)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            var properties = workbook.Properties;

            if (!string.IsNullOrWhiteSpace(properties.Title))
            {
                metadata["Title"] = properties.Title;
            }

            if (!string.IsNullOrWhiteSpace(properties.Author))
            {
                metadata["Author"] = properties.Author;
            }

            if (!string.IsNullOrWhiteSpace(properties.Subject))
            {
                metadata["Subject"] = properties.Subject;
            }

            if (properties.Created != default(DateTime))
            {
                metadata["Created"] = properties.Created.ToString("O");
            }

            if (properties.Modified != default(DateTime))
            {
                metadata["Modified"] = properties.Modified.ToString("O");
            }

            // Count worksheets
            metadata["SheetCount"] = workbook.Worksheets.Count.ToString();

            // List worksheet names
            var sheetNames = string.Join(", ", workbook.Worksheets.Select(w => w.Name));
            if (!string.IsNullOrWhiteSpace(sheetNames))
            {
                metadata["SheetNames"] = sheetNames;
            }
        }
        catch (Exception ex)
        {
            // Metadata extraction is not critical
            Console.WriteLine($"Warning: Failed to extract Excel metadata: {ex.Message}");
        }

        return metadata;
    }
}
