using Aegis.Infrastructure.Services.DocumentParsing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.DocumentParsing;

public class DocxDocumentParserTests
{
    private readonly DocxDocumentParser _parser = new(NullLogger<DocxDocumentParser>.Instance);

    [Fact]
    public async Task ParseAsync_WithValidDocx_ShouldReturnParsedText()
    {
        // Arrange
        var stream = CreateSampleDocx("This is a test Word document with multiple paragraphs.");

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("test Word document");
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
    public async Task ParseAsync_WithInvalidDocx_ShouldReturnFailure()
    {
        // Arrange
        var invalidData = "This is not a DOCX file"u8.ToArray();
        using var stream = new MemoryStream(invalidData);

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidFormat");
    }

    [Fact]
    public async Task ParseAsync_WithValidDocx_ShouldExtractMetadata()
    {
        // Arrange
        var stream = CreateSampleDocx("Test content", "Test Title", "Test Author", "Test Subject");

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata.Should().ContainKey("Title");
        result.Value.Metadata["Title"].Should().Be("Test Title");
        result.Value.Metadata.Should().ContainKey("Creator");
        result.Value.Metadata["Creator"].Should().Be("Test Author");
        result.Value.Metadata.Should().ContainKey("Subject");
        result.Value.Metadata["Subject"].Should().Be("Test Subject");
    }

    [Fact]
    public async Task ParseAsync_WithMultipleParagraphs_ShouldCombineText()
    {
        // Arrange
        var stream = CreateSampleDocx(new[] { "First paragraph", "Second paragraph", "Third paragraph" });

        // Act
        var result = await _parser.ParseAsync(stream, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("First paragraph");
        result.Value.Text.Should().Contain("Second paragraph");
        result.Value.Text.Should().Contain("Third paragraph");
    }

    [Fact]
    public void SupportedExtensions_ShouldIncludeDocx()
    {
        // Act
        var extensions = _parser.SupportedExtensions;

        // Assert
        extensions.Should().Contain(".docx");
    }

    private static MemoryStream CreateSampleDocx(string content, string? title = null, string? author = null, string? subject = null)
    {
        return CreateSampleDocx(new[] { content }, title, author, subject);
    }

    private static MemoryStream CreateSampleDocx(string[] paragraphs, string? title = null, string? author = null, string? subject = null)
    {
        var stream = new MemoryStream();

        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Add paragraphs
            foreach (var paragraphText in paragraphs)
            {
                var paragraph = body.AppendChild(new Paragraph());
                var run = paragraph.AppendChild(new Run());
                run.AppendChild(new Text(paragraphText));
            }

            // Set metadata
            if (title != null || author != null || subject != null)
            {
                var coreFilePropertiesPart = wordDocument.AddCoreFilePropertiesPart();
                using var writer = new System.Xml.XmlTextWriter(coreFilePropertiesPart.GetStream(FileMode.Create), System.Text.Encoding.UTF8);

                writer.WriteStartDocument();
                writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                writer.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
                writer.WriteAttributeString("xmlns", "dcmitype", null, "http://purl.org/dc/dcmitype/");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

                if (title != null)
                {
                    writer.WriteElementString("dc", "title", null, title);
                }

                if (author != null)
                {
                    writer.WriteElementString("dc", "creator", null, author);
                }

                if (subject != null)
                {
                    writer.WriteElementString("dc", "subject", null, subject);
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        stream.Position = 0;
        return stream;
    }
}
