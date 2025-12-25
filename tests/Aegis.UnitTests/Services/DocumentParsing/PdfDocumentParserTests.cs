using Aegis.Infrastructure.Services.DocumentParsing;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.DocumentParsing;

public class PdfDocumentParserTests
{
    private readonly PdfDocumentParser _parser = new(NullLogger<PdfDocumentParser>.Instance);

    [Fact]
    public async Task ParseAsync_WithValidPdf_ShouldReturnParsedText()
    {
        // Arrange
        var stream = CreateSamplePdf("This is a test PDF document.");

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("test PDF document");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyStream_ShouldReturnFailure()
    {
        // Arrange
        using var emptyStream = new MemoryStream();

        // Act
        var result = await _parser.ParseAsync(emptyStream, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ParseAsync_WithInvalidPdf_ShouldReturnFailure()
    {
        // Arrange
        var invalidData = "This is not a PDF file"u8.ToArray();
        using var stream = new MemoryStream(invalidData);

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidFormat");
    }

    [Fact]
    public async Task ParseAsync_WithValidPdf_ShouldExtractMetadata()
    {
        // Arrange
        var stream = CreateSamplePdf("Test content", "Test Title", "Test Author");

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata.Should().ContainKey("PageCount");
        result.Value.Metadata["PageCount"].Should().Be("1");
        result.Value.Metadata.Should().ContainKey("Title");
        result.Value.Metadata["Title"].Should().Be("Test Title");
        result.Value.Metadata.Should().ContainKey("Author");
        result.Value.Metadata["Author"].Should().Be("Test Author");
    }

    [Fact]
    public void SupportedExtensions_ShouldIncludePdf()
    {
        // Act
        var extensions = _parser.SupportedExtensions;

        // Assert
        extensions.Should().Contain(".pdf");
    }

    private static MemoryStream CreateSamplePdf(string content, string? title = null, string? author = null)
    {
        var stream = new MemoryStream();
        var writer = new PdfWriter(stream);
        writer.SetCloseStream(false); // Keep stream open after PdfDocument is closed

        var pdfDoc = new PdfDocument(writer);

        // Set metadata if provided
        if (!string.IsNullOrEmpty(title))
            pdfDoc.GetDocumentInfo().SetTitle(title);

        if (!string.IsNullOrEmpty(author))
            pdfDoc.GetDocumentInfo().SetAuthor(author);

        var document = new Document(pdfDoc);
        document.Add(new Paragraph(content));
        document.Close();

        stream.Position = 0;
        return stream;
    }
}
