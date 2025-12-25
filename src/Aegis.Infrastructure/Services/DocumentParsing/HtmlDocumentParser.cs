using Aegis.Domain.Common;
using Aegis.Domain.Services;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Aegis.Infrastructure.Services.DocumentParsing;

public class HtmlDocumentParser : IDocumentParser
{
    private readonly ILogger<HtmlDocumentParser> _logger;

    public HtmlDocumentParser(ILogger<HtmlDocumentParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => new[] { ".html", ".htm" };

    public async Task<Result<ParsedDocument>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.NullStream", "Stream cannot be null"));
        }

        try
        {
            // Create AngleSharp context
            var context = BrowsingContext.New(Configuration.Default);

            // Parse HTML from stream
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync(cancellationToken);
            var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

            // Extract text content
            var text = ExtractText(document);

            // Extract metadata
            var metadata = ExtractMetadata(document);

            return Result<ParsedDocument>.Success(new ParsedDocument
            {
                Text = text,
                Metadata = metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML document");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "Failed to parse HTML document"));
        }
    }

    private static string ExtractText(IDocument document)
    {
        // Remove script and style elements
        var scriptsAndStyles = document.QuerySelectorAll("script, style");
        foreach (var element in scriptsAndStyles)
        {
            element.Remove();
        }

        // Get text content from body
        var body = document.Body;
        if (body == null)
        {
            return string.Empty;
        }

        var textBuilder = new StringBuilder();
        ExtractTextFromNode(body, textBuilder);

        return textBuilder.ToString().Trim();
    }

    private static void ExtractTextFromNode(INode node, StringBuilder textBuilder)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == NodeType.Text)
            {
                var text = child.TextContent.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textBuilder.AppendLine(text);
                }
            }
            else if (child.NodeType == NodeType.Element)
            {
                var element = child as IElement;
                if (element == null)
                    continue;

                // Add spacing for block elements
                var isBlockElement = IsBlockElement(element.TagName);

                if (isBlockElement && textBuilder.Length > 0)
                {
                    textBuilder.AppendLine();
                }

                ExtractTextFromNode(child, textBuilder);

                if (isBlockElement && textBuilder.Length > 0)
                {
                    textBuilder.AppendLine();
                }
            }
        }
    }

    private static bool IsBlockElement(string tagName)
    {
        var blockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "div", "p", "h1", "h2", "h3", "h4", "h5", "h6",
            "ul", "ol", "li", "table", "tr", "td", "th",
            "section", "article", "header", "footer", "nav",
            "blockquote", "pre", "hr"
        };

        return blockElements.Contains(tagName);
    }

    private static Dictionary<string, string> ExtractMetadata(IDocument document)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            // Extract title
            var title = document.Title;
            if (!string.IsNullOrWhiteSpace(title))
            {
                metadata["Title"] = title;
            }

            // Extract meta tags
            var metaTags = document.QuerySelectorAll("meta");
            foreach (var metaTag in metaTags)
            {
                var name = metaTag.GetAttribute("name");
                var content = metaTag.GetAttribute("content");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content))
                    continue;

                // Capitalize first letter of meta name for consistency
                var key = char.ToUpper(name[0]) + name.Substring(1).ToLower();

                // Special handling for common meta tags
                if (name.Equals("description", StringComparison.OrdinalIgnoreCase))
                {
                    metadata["Description"] = content;
                }
                else if (name.Equals("author", StringComparison.OrdinalIgnoreCase))
                {
                    metadata["Author"] = content;
                }
                else if (name.Equals("keywords", StringComparison.OrdinalIgnoreCase))
                {
                    metadata["Keywords"] = content;
                }
                else
                {
                    metadata[key] = content;
                }
            }

            // Add format metadata
            metadata["Format"] = "HTML";
        }
        catch (Exception ex)
        {
            // Metadata extraction is not critical
            Console.WriteLine($"Warning: Failed to extract HTML metadata: {ex.Message}");
        }

        return metadata;
    }
}
