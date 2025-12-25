using Aegis.Domain.Common;
using Aegis.Domain.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.Logging;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;

namespace Aegis.Infrastructure.Services.DocumentParsing;

public class PptxDocumentParser : IDocumentParser
{
    private readonly ILogger<PptxDocumentParser> _logger;

    public PptxDocumentParser(ILogger<PptxDocumentParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => new[] { ".pptx" };

    public async Task<Result<ParsedDocument>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.NullStream", "Stream cannot be null"));
        }

        try
        {
            return await Task.Run(() =>
            {
                using var presentation = PresentationDocument.Open(stream, false);

                var text = ExtractText(presentation);
                var metadata = ExtractMetadata(presentation);

                return Result<ParsedDocument>.Success(new ParsedDocument
                {
                    Text = text,
                    Metadata = metadata
                });
            }, cancellationToken);
        }
        catch (System.IO.FileFormatException ex)
        {
            _logger.LogError(ex, "Failed to parse PPTX - invalid file format");
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.InvalidFormat", "The file is not a valid PPTX document"));
        }
        catch (OpenXmlPackageException ex)
        {
            _logger.LogError(ex, "Failed to parse PPTX - corrupt file");
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.InvalidFormat", "The PPTX file is corrupted or invalid"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PPTX document");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "Failed to parse PPTX document"));
        }
    }

    private static string ExtractText(PresentationDocument presentation)
    {
        if (presentation.PresentationPart == null)
        {
            return string.Empty;
        }

        var textBuilder = new StringBuilder();
        var slideIdList = presentation.PresentationPart.Presentation.SlideIdList;

        if (slideIdList == null)
        {
            return string.Empty;
        }

        foreach (var slideIdElement in slideIdList.ChildElements.OfType<SlideId>())
        {
            if (slideIdElement.RelationshipId == null || string.IsNullOrEmpty(slideIdElement.RelationshipId.Value))
            {
                continue;
            }

            var relationshipId = slideIdElement.RelationshipId.Value;

            var slidePart = (SlidePart)presentation.PresentationPart.GetPartById(relationshipId);

            if (slidePart?.Slide == null)
            {
                continue;
            }

            // Extract text from all text elements in the slide
            var textElements = slidePart.Slide.Descendants<A.Text>();
            foreach (var textElement in textElements)
            {
                if (!string.IsNullOrWhiteSpace(textElement.Text))
                {
                    textBuilder.AppendLine(textElement.Text);
                }
            }
        }

        return textBuilder.ToString().Trim();
    }

    private static Dictionary<string, string> ExtractMetadata(PresentationDocument presentation)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            var properties = presentation.PackageProperties;

            if (!string.IsNullOrWhiteSpace(properties.Title))
            {
                metadata["Title"] = properties.Title;
            }

            if (!string.IsNullOrWhiteSpace(properties.Creator))
            {
                metadata["Author"] = properties.Creator;
            }

            if (!string.IsNullOrWhiteSpace(properties.Subject))
            {
                metadata["Subject"] = properties.Subject;
            }

            if (properties.Created.HasValue)
            {
                metadata["Created"] = properties.Created.Value.ToString("O");
            }

            if (properties.Modified.HasValue)
            {
                metadata["Modified"] = properties.Modified.Value.ToString("O");
            }

            // Count slides
            if (presentation.PresentationPart?.Presentation?.SlideIdList != null)
            {
                var slideCount = presentation.PresentationPart.Presentation.SlideIdList.ChildElements.Count;
                metadata["SlideCount"] = slideCount.ToString();
            }
        }
        catch (Exception ex)
        {
            // Metadata extraction is not critical, just log and continue
            // Using a default logger since we don't have access to the instance logger here
            Console.WriteLine($"Warning: Failed to extract PPTX metadata: {ex.Message}");
        }

        return metadata;
    }
}
