using Aegis.Domain.Common;
using Aegis.Domain.Services;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.DocumentParsing;

public class PdfDocumentParser : IDocumentParser
{
    private readonly ILogger<PdfDocumentParser> _logger;

    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".pdf" };

    public PdfDocumentParser(ILogger<PdfDocumentParser> logger)
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

            // Parse PDF
            using var pdfReader = new PdfReader(stream);
            using var pdfDocument = new PdfDocument(pdfReader);

            var text = await Task.Run(() => ExtractText(pdfDocument), cancellationToken);
            var metadata = ExtractMetadata(pdfDocument);

            var parsedDocument = new ParsedDocument
            {
                Text = text,
                Metadata = metadata
            };

            _logger.LogInformation(
                "Successfully parsed PDF with {PageCount} pages and {CharCount} characters",
                pdfDocument.GetNumberOfPages(),
                parsedDocument.CharacterCount);

            return Result<ParsedDocument>.Success(parsedDocument);
        }
        catch (iText.IO.Exceptions.IOException ex)
        {
            _logger.LogError(ex, "Failed to parse PDF - invalid format");
            return Result<ParsedDocument>.Failure(
                Error.Validation("DocumentParser.InvalidFormat", "The file is not a valid PDF document"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing PDF");
            return Result<ParsedDocument>.Failure(
                Error.Internal("DocumentParser.ParseError", "An error occurred while parsing the PDF document"));
        }
    }

    private static string ExtractText(PdfDocument pdfDocument)
    {
        var text = new System.Text.StringBuilder();

        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);
            var strategy = new LocationTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

            text.AppendLine(pageText);
        }

        return text.ToString().Trim();
    }

    private static Dictionary<string, string> ExtractMetadata(PdfDocument pdfDocument)
    {
        var metadata = new Dictionary<string, string>
        {
            ["PageCount"] = pdfDocument.GetNumberOfPages().ToString()
        };

        var docInfo = pdfDocument.GetDocumentInfo();

        if (!string.IsNullOrWhiteSpace(docInfo.GetTitle()))
            metadata["Title"] = docInfo.GetTitle();

        if (!string.IsNullOrWhiteSpace(docInfo.GetAuthor()))
            metadata["Author"] = docInfo.GetAuthor();

        if (!string.IsNullOrWhiteSpace(docInfo.GetSubject()))
            metadata["Subject"] = docInfo.GetSubject();

        if (!string.IsNullOrWhiteSpace(docInfo.GetCreator()))
            metadata["Creator"] = docInfo.GetCreator();

        if (!string.IsNullOrWhiteSpace(docInfo.GetProducer()))
            metadata["Producer"] = docInfo.GetProducer();

        return metadata;
    }
}
