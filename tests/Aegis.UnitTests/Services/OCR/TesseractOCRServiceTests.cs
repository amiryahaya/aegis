using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.OCR;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.OCR;

public class TesseractOCRServiceTests
{
    private readonly string _tessDataPath;

    public TesseractOCRServiceTests()
    {
        // Tesseract requires tessdata to be present
        // In a real environment, this would be configured
        _tessDataPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
                       ?? Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
    }

    [Fact]
    public async Task ExtractTextAsync_WithSimpleTextImage_ShouldExtractText()
    {
        // Skip test if tessdata is not available
        if (!Directory.Exists(_tessDataPath) || !File.Exists(Path.Combine(_tessDataPath, "eng.traineddata")))
        {
            // Skip this test in CI/CD or environments without Tesseract data
            return;
        }

        // Arrange
        var service = new TesseractOCRService(_tessDataPath, NullLogger<TesseractOCRService>.Instance);

        // Create a simple test image with text
        // This would be a real image in practice, but for testing we'll skip if no image available
        var testImagePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ocr_test.png");

        if (!File.Exists(testImagePath))
        {
            // Skip if test image doesn't exist
            return;
        }

        using var stream = File.OpenRead(testImagePath);

        // Act
        var result = await service.ExtractTextAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullStream_ShouldReturnFailure()
    {
        // Skip test if tessdata is not available
        if (!Directory.Exists(_tessDataPath))
        {
            return;
        }

        // Arrange
        var service = new TesseractOCRService(_tessDataPath, NullLogger<TesseractOCRService>.Instance);

        // Act
        var result = await service.ExtractTextAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NullStream");
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidImageData_ShouldReturnFailure()
    {
        // Skip test if tessdata is not available
        if (!Directory.Exists(_tessDataPath))
        {
            return;
        }

        // Arrange
        var service = new TesseractOCRService(_tessDataPath, NullLogger<TesseractOCRService>.Instance);
        var invalidData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(invalidData);

        // Act
        var result = await service.ExtractTextAsync(stream);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithInvalidTessDataPath_ShouldThrowException()
    {
        // Arrange
        var invalidPath = "/nonexistent/path/to/tessdata";

        // Act & Assert
        var act = () => new TesseractOCRService(invalidPath, NullLogger<TesseractOCRService>.Instance);
        act.Should().Throw<DirectoryNotFoundException>();
    }
}
