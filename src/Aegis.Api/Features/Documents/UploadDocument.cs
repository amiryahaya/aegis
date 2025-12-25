using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Documents;

public static class UploadDocument
{
    public record Command : IRequest<Result<Response>>
    {
        public required Guid TeamId { get; init; }
        public required Guid DataSourceId { get; init; }
        public required Guid UserId { get; init; }
        public required string FileName { get; init; }
        public required Stream FileStream { get; init; }
        public required string ContentType { get; init; }
        public required long FileSize { get; init; }
    }

    public record Response
    {
        public required Guid DocumentId { get; init; }
        public required string FileName { get; init; }
        public required int ChunkCount { get; init; }
        public required DateTime UploadedAt { get; init; }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IDataSourceRepository _dataSourceRepository;
        private readonly IDocumentParser _pdfParser;
        private readonly IDocumentParser _docxParser;
        private readonly IDocumentParser _pptxParser;
        private readonly IDocumentParser _spreadsheetParser;
        private readonly IDocumentParser _htmlParser;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;
        private readonly IBM25Indexer _bm25Indexer;

        public Handler(
            IDocumentRepository documentRepository,
            IDataSourceRepository dataSourceRepository,
            IEnumerable<IDocumentParser> parsers,
            ITextChunker textChunker,
            IEmbeddingService embeddingService,
            IVectorStore vectorStore,
            IBM25Indexer bm25Indexer)
        {
            _documentRepository = documentRepository;
            _dataSourceRepository = dataSourceRepository;
            _textChunker = textChunker;
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
            _bm25Indexer = bm25Indexer;

            // Find parsers by supported extensions
            _pdfParser = parsers.First(p => p.SupportedExtensions.Contains(".pdf"));
            _docxParser = parsers.First(p => p.SupportedExtensions.Contains(".docx"));
            _pptxParser = parsers.First(p => p.SupportedExtensions.Contains(".pptx"));
            _spreadsheetParser = parsers.First(p => p.SupportedExtensions.Contains(".xlsx"));
            _htmlParser = parsers.First(p => p.SupportedExtensions.Contains(".html"));
        }

        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Verify data source exists and belongs to team
            var dataSource = await _dataSourceRepository.GetByIdAsync(request.DataSourceId, cancellationToken);
            if (dataSource == null)
            {
                return Result<Response>.Failure(
                    Error.NotFound("Document.DataSourceNotFound", "Data source not found"));
            }

            if (dataSource.TeamId != request.TeamId)
            {
                return Result<Response>.Failure(
                    Error.Forbidden("Document.DataSourceNotInTeam", "Data source does not belong to the specified team"));
            }

            // Select parser based on file extension
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            IDocumentParser? parser = extension switch
            {
                ".pdf" => _pdfParser,
                ".docx" => _docxParser,
                ".pptx" => _pptxParser,
                ".xlsx" => _spreadsheetParser,
                ".csv" => _spreadsheetParser,
                ".html" => _htmlParser,
                ".htm" => _htmlParser,
                _ => null
            };

            if (parser == null)
            {
                return Result<Response>.Failure(
                    Error.Validation("Document.UnsupportedFileType", $"File type '{extension}' is not supported"));
            }

            // Parse document
            var parseResult = await parser.ParseAsync(request.FileStream, cancellationToken);
            if (parseResult.IsFailure)
            {
                return Result<Response>.Failure(parseResult.Error!);
            }

            var parsedDocument = parseResult.Value;

            // Create document entity
            var document = Document.Create(
                request.FileName,
                request.ContentType,
                request.FileSize,
                request.DataSourceId,
                request.UserId,
                parsedDocument.Metadata.GetValueOrDefault("Title", request.FileName));

            // Mark as processing
            document.MarkProcessing();

            // Save document to database
            await _documentRepository.AddAsync(document, cancellationToken);

            // Chunk the text
            var chunks = await _textChunker.ChunkAsync(parsedDocument.Text);

            // Generate embeddings for chunks
            var chunkTexts = chunks.Select(c => c.Text).ToList();
            var embeddingsResult = await _embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);
            if (embeddingsResult.IsFailure)
            {
                return Result<Response>.Failure(embeddingsResult.Error!);
            }

            var embeddings = embeddingsResult.Value;

            // Ensure collection exists in vector store
            var collectionName = $"team_{request.TeamId}";
            var collectionExistsResult = await _vectorStore.CollectionExistsAsync(collectionName, cancellationToken);
            if (collectionExistsResult.IsSuccess && !collectionExistsResult.Value)
            {
                var createCollectionResult = await _vectorStore.CreateCollectionAsync(
                    collectionName,
                    _embeddingService.EmbeddingDimension,
                    cancellationToken);

                if (createCollectionResult.IsFailure)
                {
                    return Result<Response>.Failure(createCollectionResult.Error!);
                }
            }

            // Ensure index exists in BM25
            var indexExistsResult = await _bm25Indexer.IndexExistsAsync(collectionName, cancellationToken);
            if (indexExistsResult.IsSuccess && !indexExistsResult.Value)
            {
                var createIndexResult = await _bm25Indexer.CreateIndexAsync(collectionName, cancellationToken);
                if (createIndexResult.IsFailure)
                {
                    return Result<Response>.Failure(createIndexResult.Error!);
                }
            }

            // Index in vector store
            var vectorPoints = chunks.Select((chunk, index) => new VectorPoint
            {
                Id = Guid.NewGuid(),
                Vector = embeddings[index],
                Metadata = new Dictionary<string, string>
                {
                    ["documentId"] = document.Id.ToString(),
                    ["dataSourceId"] = request.DataSourceId.ToString(),
                    ["text"] = chunk.Text,
                    ["chunkIndex"] = index.ToString()
                }
            }).ToList();

            var vectorUpsertResult = await _vectorStore.UpsertBatchAsync(collectionName, vectorPoints, cancellationToken);
            if (vectorUpsertResult.IsFailure)
            {
                return Result<Response>.Failure(vectorUpsertResult.Error!);
            }

            // Index in BM25
            var bm25Documents = chunks.Select((chunk, index) => new BM25Document
            {
                Id = vectorPoints[index].Id,
                Text = chunk.Text,
                Metadata = new Dictionary<string, string>
                {
                    ["documentId"] = document.Id.ToString(),
                    ["dataSourceId"] = request.DataSourceId.ToString(),
                    ["text"] = chunk.Text,
                    ["chunkIndex"] = index.ToString()
                }
            }).ToList();

            var bm25IndexResult = await _bm25Indexer.IndexBatchAsync(collectionName, bm25Documents, cancellationToken);
            if (bm25IndexResult.IsFailure)
            {
                // Mark document as failed
                document.MarkFailed(bm25IndexResult.Error!.Message);
                await _documentRepository.UpdateAsync(document, cancellationToken);
                return Result<Response>.Failure(bm25IndexResult.Error!);
            }

            // Mark document as processed
            document.MarkProcessed(chunks.Count);
            await _documentRepository.UpdateAsync(document, cancellationToken);

            return Result<Response>.Success(new Response
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                ChunkCount = chunks.Count,
                UploadedAt = document.CreatedAt
            });
        }
    }
}
