using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Dapper;
using Npgsql;

namespace Aegis.Infrastructure.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly string _connectionString;

    public DocumentRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, file_name, title, content_type, size_bytes, storage_path, content, external_id,
                   data_source_id, uploaded_by, status, error_message, chunk_count, processed_at, metadata,
                   created_at, updated_at
            FROM documents
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<DocumentRecord>(sql, new { Id = id });

        return record?.ToDocument();
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, file_name, title, content_type, size_bytes, storage_path, content, external_id,
                   data_source_id, uploaded_by, status, error_message, chunk_count, processed_at, metadata,
                   created_at, updated_at
            FROM documents
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DocumentRecord>(sql);

        return records.Select(r => r.ToDocument()).ToList();
    }

    public async Task<IReadOnlyList<Document>> GetByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, file_name, title, content_type, size_bytes, storage_path, content, external_id,
                   data_source_id, uploaded_by, status, error_message, chunk_count, processed_at, metadata,
                   created_at, updated_at
            FROM documents
            WHERE data_source_id = @DataSourceId
            ORDER BY created_at DESC
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DocumentRecord>(sql, new { DataSourceId = dataSourceId });

        return records.Select(r => r.ToDocument()).ToList();
    }

    public async Task<Document?> GetByExternalIdAsync(Guid dataSourceId, string externalId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, file_name, title, content_type, size_bytes, storage_path, content, external_id,
                   data_source_id, uploaded_by, status, error_message, chunk_count, processed_at, metadata,
                   created_at, updated_at
            FROM documents
            WHERE data_source_id = @DataSourceId AND external_id = @ExternalId
            """;

        await using var connection = CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<DocumentRecord>(sql, new { DataSourceId = dataSourceId, ExternalId = externalId });

        return record?.ToDocument();
    }

    public async Task<IReadOnlyList<Document>> GetPendingDocumentsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, file_name, title, content_type, size_bytes, storage_path, content, external_id,
                   data_source_id, uploaded_by, status, error_message, chunk_count, processed_at, metadata,
                   created_at, updated_at
            FROM documents
            WHERE status = 'Pending'
            ORDER BY created_at ASC
            LIMIT @Limit
            """;

        await using var connection = CreateConnection();
        var records = await connection.QueryAsync<DocumentRecord>(sql, new { Limit = limit });

        return records.Select(r => r.ToDocument()).ToList();
    }

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO documents (id, file_name, title, content_type, size_bytes, storage_path, content, external_id,
                                 data_source_id, uploaded_by, status, error_message, chunk_count,
                                 processed_at, metadata, created_at, updated_at)
            VALUES (@Id, @FileName, @Title, @ContentType, @SizeBytes, @StoragePath, @Content, @ExternalId,
                    @DataSourceId, @UploadedBy, @Status, @ErrorMessage, @ChunkCount,
                    @ProcessedAt, @Metadata::jsonb, @CreatedAt, @UpdatedAt)
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            document.Id,
            document.FileName,
            document.Title,
            document.ContentType,
            document.SizeBytes,
            document.StoragePath,
            document.Content,
            document.ExternalId,
            document.DataSourceId,
            document.UploadedBy,
            Status = document.Status.ToString(),
            document.ErrorMessage,
            document.ChunkCount,
            document.ProcessedAt,
            Metadata = JsonSerializer.Serialize(document.Metadata),
            document.CreatedAt,
            UpdatedAt = document.UpdatedAt ?? document.CreatedAt
        });
    }

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE documents
            SET title = @Title,
                content = @Content,
                size_bytes = @SizeBytes,
                status = @Status,
                error_message = @ErrorMessage,
                chunk_count = @ChunkCount,
                processed_at = @ProcessedAt,
                metadata = @Metadata::jsonb,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            document.Id,
            document.Title,
            document.Content,
            document.SizeBytes,
            Status = document.Status.ToString(),
            document.ErrorMessage,
            document.ChunkCount,
            document.ProcessedAt,
            Metadata = JsonSerializer.Serialize(document.Metadata),
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM documents WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM documents WHERE id = @Id)";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = id });
    }

    public async Task<int> GetCountByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM documents WHERE data_source_id = @DataSourceId";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { DataSourceId = dataSourceId });
    }

    private record DocumentRecord(
        Guid Id,
        string File_Name,
        string? Title,
        string Content_Type,
        long Size_Bytes,
        string? Storage_Path,
        string? Content,
        string? External_Id,
        Guid Data_Source_Id,
        Guid Uploaded_By,
        string Status,
        string? Error_Message,
        int Chunk_Count,
        DateTime? Processed_At,
        string Metadata,
        DateTime Created_At,
        DateTime? Updated_At)
    {
        public Document ToDocument()
        {
            var status = Enum.Parse<DocumentStatus>(Status, ignoreCase: true);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata) ?? new Dictionary<string, string>();

            return Document.Reconstitute(
                Id,
                File_Name,
                Title,
                Content_Type,
                Size_Bytes,
                Storage_Path,
                Content,
                External_Id,
                Data_Source_Id,
                Uploaded_By,
                status,
                Error_Message,
                Chunk_Count,
                Processed_At,
                metadata,
                Created_At,
                Updated_At);
        }
    }
}
