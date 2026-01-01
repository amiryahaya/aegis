using System.Diagnostics;
using Aegis.Domain.Common;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Jobs;

/// <summary>
/// Background job for processing documents (parsing, chunking, embedding)
/// </summary>
public class DocumentProcessingJob : IDocumentProcessingJob
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<DocumentProcessingJob> _logger;

    public DocumentProcessingJob(
        IDocumentRepository documentRepository,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<DocumentProcessingJob> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    [Queue("default")]
    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting document processing job for document {DocumentId}", jobId);

        var result = await ProcessDocumentAsync(jobId, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Document processing failed for {DocumentId}: {Error}",
                jobId, result.Error?.Message);
            throw new InvalidOperationException($"Document processing failed: {result.Error?.Message}");
        }

        _logger.LogInformation("Document processing completed for {DocumentId}: {ChunksCreated} chunks, {EmbeddingsGenerated} embeddings in {ProcessingTime}",
            jobId, result.Value.ChunksCreated, result.Value.EmbeddingsGenerated, result.Value.ProcessingTime);
    }

    public async Task<Result<DocumentProcessingResult>> ProcessDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Fetch the document
            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (document == null)
            {
                return Result<DocumentProcessingResult>.Failure(
                    Error.NotFound("Document.NotFound", $"Document {documentId} not found"));
            }

            // Update status to parsing
            await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Parsing, cancellationToken);

            // Get document content
            var content = document.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Result<DocumentProcessingResult>.Failure(
                    Error.Validation("Document.EmptyContent", "Document has no content to process"));
            }

            // Chunk the content
            await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Chunking, cancellationToken);
            var chunkResult = await _chunkingService.ChunkTextAsync(content, cancellationToken);
            if (chunkResult.IsFailure)
            {
                await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Failed, cancellationToken);
                return Result<DocumentProcessingResult>.Failure(chunkResult.Error!);
            }

            var chunks = chunkResult.Value;
            _logger.LogDebug("Created {ChunkCount} chunks for document {DocumentId}", chunks.Count, documentId);

            // Generate embeddings
            await UpdateDocumentStatus(documentId, DocumentProcessingStatus.GeneratingEmbeddings, cancellationToken);
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(
                chunks.Select(c => c.Text).ToList(),
                cancellationToken);

            if (embeddingResult.IsFailure)
            {
                await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Failed, cancellationToken);
                return Result<DocumentProcessingResult>.Failure(embeddingResult.Error!);
            }

            var embeddings = embeddingResult.Value;
            _logger.LogDebug("Generated {EmbeddingCount} embeddings for document {DocumentId}",
                embeddings.Count, documentId);

            // Store in vector database
            await UpdateDocumentStatus(documentId, DocumentProcessingStatus.StoringVectors, cancellationToken);
            var vectorData = chunks.Zip(embeddings, (chunk, embedding) => new VectorDocument
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = chunk.Index,
                Text = chunk.Text,
                Embedding = embedding,
                Metadata = new Dictionary<string, object>
                {
                    ["document_id"] = documentId.ToString(),
                    ["chunk_index"] = chunk.Index,
                    ["start_position"] = chunk.StartPosition,
                    ["end_position"] = chunk.EndPosition
                }
            }).ToList();

            var storeResult = await _vectorStore.UpsertAsync(vectorData, cancellationToken);
            if (storeResult.IsFailure)
            {
                await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Failed, cancellationToken);
                return Result<DocumentProcessingResult>.Failure(storeResult.Error!);
            }

            // Update document status to completed
            await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Completed, cancellationToken);

            stopwatch.Stop();

            return Result<DocumentProcessingResult>.Success(new DocumentProcessingResult(
                documentId,
                chunks.Count,
                embeddings.Count,
                stopwatch.Elapsed,
                DocumentProcessingStatus.Completed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", documentId);
            await UpdateDocumentStatus(documentId, DocumentProcessingStatus.Failed, cancellationToken);

            stopwatch.Stop();

            return Result<DocumentProcessingResult>.Success(new DocumentProcessingResult(
                documentId,
                0,
                0,
                stopwatch.Elapsed,
                DocumentProcessingStatus.Failed,
                ex.Message));
        }
    }

    private async Task UpdateDocumentStatus(
        Guid documentId,
        DocumentProcessingStatus status,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Updating document {DocumentId} status to {Status}", documentId, status);
        // In a real implementation, this would update the document's processing status
        await Task.CompletedTask;
    }
}

/// <summary>
/// Represents a document chunk
/// </summary>
public record TextChunk(int Index, string Text, int StartPosition, int EndPosition);

/// <summary>
/// Service for chunking text content
/// </summary>
public interface IChunkingService
{
    Task<Result<List<TextChunk>>> ChunkTextAsync(string content, CancellationToken cancellationToken);
}

/// <summary>
/// Service for generating embeddings
/// </summary>
public interface IEmbeddingService
{
    Task<Result<List<float[]>>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken);
}

/// <summary>
/// Vector store interface
/// </summary>
public interface IVectorStore
{
    Task<Result<int>> UpsertAsync(List<VectorDocument> documents, CancellationToken cancellationToken);
}

/// <summary>
/// Document to store in vector database
/// </summary>
public class VectorDocument
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = [];
}
