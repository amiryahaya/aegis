using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for extracting structured table data from documents
/// </summary>
public interface ITableExtractor
{
    /// <summary>
    /// Extracts tables from HTML content
    /// </summary>
    /// <param name="html">The HTML content containing tables</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing a list of extracted tables</returns>
    Task<Result<IReadOnlyList<ExtractedTable>>> ExtractFromHtmlAsync(string html, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a table extracted from a document
/// </summary>
public class ExtractedTable
{
    /// <summary>
    /// Headers of the table (first row)
    /// </summary>
    public required IReadOnlyList<string> Headers { get; init; }

    /// <summary>
    /// Rows of data in the table
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>
    /// Gets the table formatted as markdown
    /// </summary>
    public string ToMarkdown()
    {
        if (Rows.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();

        // Add headers
        if (Headers.Count > 0)
        {
            sb.AppendLine("| " + string.Join(" | ", Headers) + " |");
            sb.AppendLine("|" + string.Join("|", Headers.Select(_ => "---")) + "|");
        }

        // Add rows
        foreach (var row in Rows)
        {
            sb.AppendLine("| " + string.Join(" | ", row) + " |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the table formatted as plain text with tab separators
    /// </summary>
    public string ToPlainText()
    {
        if (Rows.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();

        // Add headers
        if (Headers.Count > 0)
        {
            sb.AppendLine(string.Join("\t", Headers));
            sb.AppendLine(new string('-', 50));
        }

        // Add rows
        foreach (var row in Rows)
        {
            sb.AppendLine(string.Join("\t", row));
        }

        return sb.ToString();
    }
}
