using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for performing Optical Character Recognition (OCR) on images
/// </summary>
public interface IOCRService
{
    /// <summary>
    /// Extracts text from an image using OCR
    /// </summary>
    /// <param name="imageStream">The image stream to process</param>
    /// <param name="language">The language code (e.g., "eng" for English)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the extracted text</returns>
    Task<Result<string>> ExtractTextAsync(Stream imageStream, string language = "eng", CancellationToken cancellationToken = default);
}
