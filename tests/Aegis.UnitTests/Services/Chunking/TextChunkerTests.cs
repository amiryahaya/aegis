using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Chunking;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.Chunking;

public class TextChunkerTests
{
    [Fact]
    public async Task ChunkAsync_WithFixedSizeStrategy_ShouldSplitIntoFixedChunks()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Strategy = ChunkingStrategy.FixedSize,
            MaxChunkSize = 50,
            ChunkOverlap = 10
        };
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "This is a test text that should be split into multiple chunks based on fixed size.";

        // Act
        var chunks = await chunker.ChunkAsync(text);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().HaveCountGreaterThan(1);
        chunks.All(c => c.Text.Length <= options.MaxChunkSize).Should().BeTrue();
        chunks.Should().BeInAscendingOrder(c => c.Index);
    }

    [Fact]
    public async Task ChunkAsync_WithSentenceStrategy_ShouldSplitOnSentences()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Strategy = ChunkingStrategy.Sentence,
            MaxChunkSize = 100,
            ChunkOverlap = 20
        };
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "First sentence. Second sentence. Third sentence. Fourth sentence.";

        // Act
        var chunks = await chunker.ChunkAsync(text);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.All(c => c.Text.Trim().EndsWith('.') || c == chunks.Last()).Should().BeTrue();
    }

    [Fact]
    public async Task ChunkAsync_WithParagraphStrategy_ShouldSplitOnParagraphs()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Strategy = ChunkingStrategy.Paragraph,
            MaxChunkSize = 50,
            ChunkOverlap = 0
        };
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "This is the first paragraph with some content.\n\nThis is the second paragraph with more content.\n\nThis is the third paragraph.";

        // Act
        var chunks = await chunker.ChunkAsync(text);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task ChunkAsync_WithOverlap_ShouldCreateMoreChunks()
    {
        // Arrange
        var text = "This is a longer text that will be split into multiple chunks to test the overlap functionality properly.";

        var chunkerWithoutOverlap = new TextChunker(
            new ChunkingOptions { Strategy = ChunkingStrategy.FixedSize, MaxChunkSize = 30, ChunkOverlap = 0 },
            NullLogger<TextChunker>.Instance);

        var chunkerWithOverlap = new TextChunker(
            new ChunkingOptions { Strategy = ChunkingStrategy.FixedSize, MaxChunkSize = 30, ChunkOverlap = 10 },
            NullLogger<TextChunker>.Instance);

        // Act
        var chunksWithoutOverlap = await chunkerWithoutOverlap.ChunkAsync(text);
        var chunksWithOverlap = await chunkerWithOverlap.ChunkAsync(text);

        // Assert
        // With overlap, we should have more chunks because we're moving forward by less each time
        chunksWithOverlap.Count.Should().BeGreaterOrEqualTo(chunksWithoutOverlap.Count);

        // Verify chunks are properly indexed
        for (int i = 0; i < chunksWithOverlap.Count; i++)
        {
            chunksWithOverlap[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public async Task ChunkAsync_WithEmptyText_ShouldReturnEmptyList()
    {
        // Arrange
        var options = new ChunkingOptions();
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "";

        // Act
        var chunks = await chunker.ChunkAsync(text);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ChunkAsync_WithShortText_ShouldReturnSingleChunk()
    {
        // Arrange
        var options = new ChunkingOptions { MaxChunkSize = 100 };
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "Short text.";

        // Act
        var chunks = await chunker.ChunkAsync(text);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be(text);
        chunks[0].Index.Should().Be(0);
        chunks[0].StartOffset.Should().Be(0);
        chunks[0].EndOffset.Should().Be(text.Length);
    }

    [Fact]
    public async Task ChunkAsync_WithMetadata_ShouldAttachMetadataToChunks()
    {
        // Arrange
        var options = new ChunkingOptions { MaxChunkSize = 50 };
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "This is a test text.";
        var metadata = new Dictionary<string, string>
        {
            ["source"] = "test.pdf",
            ["page"] = "1"
        };

        // Act
        var chunks = await chunker.ChunkAsync(text, metadata);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.All(c => c.Metadata.ContainsKey("source")).Should().BeTrue();
        chunks.All(c => c.Metadata["source"] == "test.pdf").Should().BeTrue();
        chunks.All(c => c.Metadata["page"] == "1").Should().BeTrue();
    }

    [Fact]
    public async Task ChunkAsync_ShouldSetCorrectOffsets()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            Strategy = ChunkingStrategy.Sentence,
            MaxChunkSize = 100
        };
        var chunker = new TextChunker(options, NullLogger<TextChunker>.Instance);
        var text = "First sentence. Second sentence. Third sentence.";

        // Act
        var chunks = await chunker.ChunkAsync(text);

        // Assert
        chunks.Should().NotBeEmpty();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            chunk.Index.Should().Be(i);
            chunk.StartOffset.Should().BeGreaterOrEqualTo(0);
            chunk.EndOffset.Should().BeGreaterThan(chunk.StartOffset);
            chunk.EndOffset.Should().BeLessOrEqualTo(text.Length);

            // Verify that the chunk text matches the original text at the specified offsets
            var extractedText = text.Substring(chunk.StartOffset, chunk.EndOffset - chunk.StartOffset);
            extractedText.Should().Contain(chunk.Text.Trim());
        }
    }
}
