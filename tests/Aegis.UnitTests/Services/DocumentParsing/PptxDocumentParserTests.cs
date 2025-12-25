using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.DocumentParsing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Aegis.UnitTests.Services.DocumentParsing;

public class PptxDocumentParserTests
{
    private readonly PptxDocumentParser _parser;

    public PptxDocumentParserTests()
    {
        _parser = new PptxDocumentParser(NullLogger<PptxDocumentParser>.Instance);
    }

    [Fact]
    public void SupportedExtensions_ShouldIncludePptx()
    {
        // Assert
        _parser.SupportedExtensions.Should().Contain(".pptx");
    }

    [Fact]
    public async Task ParseAsync_WithValidPptx_ShouldExtractText()
    {
        // Arrange
        var pptxContent = CreateTestPptx("Test slide content about machine learning");

        using var stream = new MemoryStream(pptxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("machine learning");
    }

    [Fact]
    public async Task ParseAsync_WithMultipleSlides_ShouldExtractAllText()
    {
        // Arrange
        var pptxContent = CreateTestPptxWithMultipleSlides(
            "First slide content",
            "Second slide content",
            "Third slide content");

        using var stream = new MemoryStream(pptxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Contain("First slide content");
        result.Value.Text.Should().Contain("Second slide content");
        result.Value.Text.Should().Contain("Third slide content");
    }

    [Fact]
    public async Task ParseAsync_WithValidPptx_ShouldExtractMetadata()
    {
        // Arrange
        var pptxContent = CreateTestPptxWithMetadata(
            "Test content",
            title: "Test Presentation",
            author: "Test Author");

        using var stream = new MemoryStream(pptxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("Title");
        result.Value.Metadata["Title"].Should().Be("Test Presentation");
        result.Value.Metadata.Should().ContainKey("Author");
        result.Value.Metadata["Author"].Should().Be("Test Author");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyPptx_ShouldReturnEmptyText()
    {
        // Arrange
        var pptxContent = CreateTestPptx("");

        using var stream = new MemoryStream(pptxContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithInvalidPptx_ShouldReturnFailure()
    {
        // Arrange
        var invalidContent = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(invalidContent);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidFormat");
    }

    [Fact]
    public async Task ParseAsync_WithNullStream_ShouldReturnFailure()
    {
        // Act
        var result = await _parser.ParseAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    private static byte[] CreateTestPptx(string content)
    {
        using var memoryStream = new MemoryStream();
        using var presentation = PresentationDocument.Create(memoryStream, PresentationDocumentType.Presentation);

        var presentationPart = presentation.AddPresentationPart();
        presentationPart.Presentation = new Presentation();

        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = new Slide(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    new P.Shape(
                        new P.NonVisualShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 2, Name = "TextBox" },
                            new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                            new ApplicationNonVisualDrawingProperties(new PlaceholderShape())),
                        new P.ShapeProperties(),
                        new P.TextBody(
                            new A.BodyProperties(),
                            new A.ListStyle(),
                            new A.Paragraph(new A.Run(new A.Text(content))))))),
            new ColorMapOverride(new A.MasterColorMapping()));

        var slideIdList = new SlideIdList();
        slideIdList.Append(new SlideId { Id = 256, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
        presentationPart.Presentation.SlideIdList = slideIdList;

        presentationPart.Presentation.Save();
        presentation.Dispose();

        return memoryStream.ToArray();
    }

    private static byte[] CreateTestPptxWithMultipleSlides(params string[] slideContents)
    {
        using var memoryStream = new MemoryStream();
        using var presentation = PresentationDocument.Create(memoryStream, PresentationDocumentType.Presentation);

        var presentationPart = presentation.AddPresentationPart();
        presentationPart.Presentation = new Presentation();

        var slideIdList = new SlideIdList();
        uint slideId = 256;

        foreach (var content in slideContents)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 2, Name = "TextBox" },
                                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                                new ApplicationNonVisualDrawingProperties(new PlaceholderShape())),
                            new P.ShapeProperties(),
                            new P.TextBody(
                                new A.BodyProperties(),
                                new A.ListStyle(),
                                new A.Paragraph(new A.Run(new A.Text(content))))))),
                new ColorMapOverride(new A.MasterColorMapping()));

            slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
        }

        presentationPart.Presentation.SlideIdList = slideIdList;
        presentationPart.Presentation.Save();
        presentation.Dispose();

        return memoryStream.ToArray();
    }

    private static byte[] CreateTestPptxWithMetadata(string content, string title, string author)
    {
        using var memoryStream = new MemoryStream();
        using (var presentation = PresentationDocument.Create(memoryStream, PresentationDocumentType.Presentation))
        {
            // Set core properties
            var coreProperties = presentation.PackageProperties;
            coreProperties.Title = title;
            coreProperties.Creator = author;

            var presentationPart = presentation.AddPresentationPart();
            presentationPart.Presentation = new Presentation();

            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 2, Name = "TextBox" },
                                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                                new ApplicationNonVisualDrawingProperties(new PlaceholderShape())),
                            new P.ShapeProperties(),
                            new P.TextBody(
                                new A.BodyProperties(),
                                new A.ListStyle(),
                                new A.Paragraph(new A.Run(new A.Text(content))))))),
                new ColorMapOverride(new A.MasterColorMapping()));

            var slideIdList = new SlideIdList();
            slideIdList.Append(new SlideId { Id = 256, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            presentationPart.Presentation.SlideIdList = slideIdList;

            presentationPart.Presentation.Save();
        }

        return memoryStream.ToArray();
    }
}
