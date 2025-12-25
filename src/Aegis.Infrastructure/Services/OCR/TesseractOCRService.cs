using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace Aegis.Infrastructure.Services.OCR;

public class TesseractOCRService : IOCRService
{
    private readonly string _tessDataPath;
    private readonly ILogger<TesseractOCRService> _logger;

    public TesseractOCRService(string tessDataPath, ILogger<TesseractOCRService> logger)
    {
        _logger = logger;

        // Validate tessdata path exists
        if (!Directory.Exists(tessDataPath))
        {
            throw new DirectoryNotFoundException($"Tesseract data path not found: {tessDataPath}");
        }

        _tessDataPath = tessDataPath;
    }

    public async Task<Result<string>> ExtractTextAsync(
        Stream imageStream,
        string language = "eng",
        CancellationToken cancellationToken = default)
    {
        if (imageStream == null)
        {
            return Result<string>.Failure(
                Error.Validation("OCRService.NullStream", "Image stream cannot be null"));
        }

        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert stream to byte array
                    using var memoryStream = new MemoryStream();
                    imageStream.CopyTo(memoryStream);
                    var imageBytes = memoryStream.ToArray();

                    // Initialize Tesseract engine
                    using var engine = new TesseractEngine(_tessDataPath, language, EngineMode.Default);

                    // Load image from bytes
                    using var img = Pix.LoadFromMemory(imageBytes);

                    // Perform OCR
                    using var page = engine.Process(img);
                    var text = page.GetText();

                    return Result<string>.Success(text.Trim());
                }
                catch (TesseractException ex)
                {
                    _logger.LogError(ex, "Tesseract OCR failed");
                    return Result<string>.Failure(
                        Error.Internal("OCRService.TesseractError", $"OCR processing failed: {ex.Message}"));
                }
                catch (Exception ex) when (ex.Message.Contains("Failed to load") ||
                                          ex.Message.Contains("cannot identify image file"))
                {
                    _logger.LogError(ex, "Failed to load image for OCR");
                    return Result<string>.Failure(
                        Error.Validation("OCRService.InvalidImage", "The provided data is not a valid image"));
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR processing failed");
            return Result<string>.Failure(
                Error.Internal("OCRService.ProcessError", "Failed to process image for OCR"));
        }
    }
}
