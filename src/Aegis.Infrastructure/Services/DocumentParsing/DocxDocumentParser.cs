using Aegis.Domain.Common;
using Aegis.Domain.Services;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml.Linq;

namespace Aegis.Infrastructure.Services.DocumentParsing;

public class DocxDocumentParser : IDocumentParser
{
    private readonly ILogger<DocxDocumentParser> _logger;

    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".docx" };

    public DocxDocumentParser(ILogger<DocxDocumentParser> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ParsedDocument>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate stream
            if (stream == null || stream.Length == 0)
            {
                return Result<ParsedDocument>.Failure(
                    Error.Validation("DocumentParser.EmptyStream", "The provided stream is empty"));
            }

            // Ensure stream is at the beginning
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // Parse DOCX
            using var wordDocument = WordprocessingDocument.Open(stream, false);

            if (wordDocument.MainDocumentPart == null)
            {
                return Result<ParsedDocument>.Failure(
                    Error.Validation("DocumentParser.InvalidFormat", "The DOCX file has no main document part"));
            }

            var text = await Task.Run(() => ExtractText(wordDocument), cancellationToken);
            var metadata = ExtractMetadata(wordDocument);

            var parsedDocument = new ParsedDocument
            {
                Text = text,
                Metadata = metadata
            };

            _logger.LogInformation(
                "Successfully parsed DOCX with {CharCount} characters",
                parsedDocument.CharacterCount);

            return Result<ParsedDocument>.Success(parsedDocument);
        }
        catch (OpenXmlPackageException ex)
        {
            _logger.LogError(ex, "Failed to parse DOCX - invalid format");
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.InvalidFormat", "The file is not a valid DOCX document"));
        }
        catch (System.IO.FileFormatException ex)
        {
            _logger.LogError(ex, "Failed to parse DOCX - invalid file format");
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.InvalidFormat", "The file is not a valid DOCX document"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing DOCX");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "An error occurred while parsing the DOCX document"));
        }
    }

    private static string ExtractText(WordprocessingDocument wordDocument)
    {
        var body = wordDocument.MainDocumentPart!.Document.Body;

        if (body == null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();

        // Extract text from all paragraphs
        foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            var paragraphText = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(paragraphText))
            {
                text.AppendLine(paragraphText);
            }
        }

        // Extract text from tables
        foreach (var table in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>())
        {
            foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
            {
                foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                {
                    var cellText = cell.InnerText;
                    if (!string.IsNullOrWhiteSpace(cellText))
                    {
                        text.Append(cellText).Append("\t");
                    }
                }
                text.AppendLine();
            }
        }

        return text.ToString().Trim();
    }

    private static Dictionary<string, string> ExtractMetadata(WordprocessingDocument wordDocument)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            var coreProperties = wordDocument.PackageProperties;

            if (!string.IsNullOrWhiteSpace(coreProperties.Title))
                metadata["Title"] = coreProperties.Title;

            if (!string.IsNullOrWhiteSpace(coreProperties.Creator))
                metadata["Creator"] = coreProperties.Creator;

            if (!string.IsNullOrWhiteSpace(coreProperties.Subject))
                metadata["Subject"] = coreProperties.Subject;

            if (!string.IsNullOrWhiteSpace(coreProperties.Keywords))
                metadata["Keywords"] = coreProperties.Keywords;

            if (!string.IsNullOrWhiteSpace(coreProperties.Description))
                metadata["Description"] = coreProperties.Description;

            if (!string.IsNullOrWhiteSpace(coreProperties.LastModifiedBy))
                metadata["LastModifiedBy"] = coreProperties.LastModifiedBy;

            if (coreProperties.Created.HasValue)
                metadata["Created"] = coreProperties.Created.Value.ToString("yyyy-MM-dd HH:mm:ss");

            if (coreProperties.Modified.HasValue)
                metadata["Modified"] = coreProperties.Modified.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception ex)
        {
            // Metadata extraction failed, but we can still return the text
            metadata["MetadataError"] = $"Failed to extract metadata: {ex.Message}";
        }

        return metadata;
    }
}
