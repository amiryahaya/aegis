using Aegis.Domain.Common;
using Aegis.Domain.Services;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Tables;

public class HtmlTableExtractor : ITableExtractor
{
    private readonly ILogger<HtmlTableExtractor> _logger;

    public HtmlTableExtractor(ILogger<HtmlTableExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ExtractedTable>>> ExtractFromHtmlAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (html == null)
        {
            return Result<IReadOnlyList<ExtractedTable>>.Failure(
                Error.Validation("TableExtractor.NullHtml", "HTML content cannot be null"));
        }

        try
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

            var tables = document.QuerySelectorAll("table");
            var extractedTables = new List<ExtractedTable>();

            foreach (var table in tables)
            {
                var extractedTable = ExtractTableFromElement(table);
                if (extractedTable != null)
                {
                    extractedTables.Add(extractedTable);
                }
            }

            return Result<IReadOnlyList<ExtractedTable>>.Success(extractedTables.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract tables from HTML");
            return Result<IReadOnlyList<ExtractedTable>>.Failure(
                Error.Internal("TableExtractor.ExtractionError", "Failed to extract tables from HTML"));
        }
    }

    private ExtractedTable? ExtractTableFromElement(IElement tableElement)
    {
        try
        {
            var headers = new List<string>();
            var rows = new List<IReadOnlyList<string>>();

            // Try to find headers in thead
            var thead = tableElement.QuerySelector("thead");
            if (thead != null)
            {
                var headerRow = thead.QuerySelector("tr");
                if (headerRow != null)
                {
                    headers = ExtractCellsFromRow(headerRow, "th");
                    if (headers.Count == 0)
                    {
                        // Fallback to td if no th elements
                        headers = ExtractCellsFromRow(headerRow, "td");
                    }
                }
            }

            // Extract data rows from tbody or all tr elements
            var tbody = tableElement.QuerySelector("tbody");
            var dataRows = tbody != null
                ? tbody.QuerySelectorAll("tr")
                : tableElement.QuerySelectorAll("tr");

            var startIndex = 0;

            // If no thead was found, use first row as headers
            if (headers.Count == 0 && dataRows.Length > 0)
            {
                var firstRow = dataRows[0];
                headers = ExtractCellsFromRow(firstRow, "th");

                if (headers.Count == 0)
                {
                    // If first row has no th, use td as headers
                    headers = ExtractCellsFromRow(firstRow, "td");
                }

                startIndex = 1; // Skip first row in data
            }

            // Extract data rows
            for (int i = startIndex; i < dataRows.Length; i++)
            {
                var rowData = ExtractCellsFromRow(dataRows[i], "td");
                if (rowData.Count > 0)
                {
                    rows.Add(rowData.AsReadOnly());
                }
            }

            // Skip empty tables
            if (headers.Count == 0 && rows.Count == 0)
            {
                return null;
            }

            // Ensure we have headers (use column numbers if no headers found)
            if (headers.Count == 0 && rows.Count > 0)
            {
                var columnCount = rows[0].Count;
                headers = Enumerable.Range(1, columnCount).Select(i => $"Column{i}").ToList();
            }

            return new ExtractedTable
            {
                Headers = headers.AsReadOnly(),
                Rows = rows.AsReadOnly()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract table data from element");
            return null;
        }
    }

    private List<string> ExtractCellsFromRow(IElement row, string cellTag)
    {
        var cells = row.QuerySelectorAll(cellTag);
        return cells
            .Select(cell => cell.TextContent.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }
}
