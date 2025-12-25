using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Services.Chunking;

public class TextChunker : ITextChunker
{
    private readonly ChunkingOptions _options;
    private readonly ILogger<TextChunker> _logger;

    // Regex patterns for splitting
    private static readonly Regex SentencePattern = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
    private static readonly Regex ParagraphPattern = new(@"\n\s*\n", RegexOptions.Compiled);

    public TextChunker(ChunkingOptions options, ILogger<TextChunker> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<List<TextChunk>> ChunkAsync(string text, Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<TextChunk>();
        }

        var chunks = await Task.Run(() =>
        {
            return _options.Strategy switch
            {
                ChunkingStrategy.FixedSize => ChunkByFixedSize(text),
                ChunkingStrategy.Sentence => ChunkBySentence(text),
                ChunkingStrategy.Paragraph => ChunkByParagraph(text),
                ChunkingStrategy.Semantic => throw new NotImplementedException("Semantic chunking requires embeddings and will be implemented in the next phase"),
                _ => throw new ArgumentException($"Unknown chunking strategy: {_options.Strategy}")
            };
        });

        // Attach metadata to all chunks
        if (metadata != null)
        {
            foreach (var chunk in chunks)
            {
                foreach (var kvp in metadata)
                {
                    chunk.Metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        _logger.LogInformation(
            "Chunked text into {ChunkCount} chunks using {Strategy} strategy",
            chunks.Count,
            _options.Strategy);

        return chunks;
    }

    private List<TextChunk> ChunkByFixedSize(string text)
    {
        var chunks = new List<TextChunk>();
        var currentOffset = 0;
        var chunkIndex = 0;

        while (currentOffset < text.Length)
        {
            var chunkSize = Math.Min(_options.MaxChunkSize, text.Length - currentOffset);
            var chunkText = text.Substring(currentOffset, chunkSize);

            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = chunkIndex++,
                StartOffset = currentOffset,
                EndOffset = currentOffset + chunkSize
            });

            // Move forward by chunk size minus overlap
            currentOffset += Math.Max(1, chunkSize - _options.ChunkOverlap);
        }

        return chunks;
    }

    private List<TextChunk> ChunkBySentence(string text)
    {
        var sentences = SentencePattern.Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var chunks = new List<TextChunk>();
        var currentChunk = new StringBuilder();
        var chunkStartOffset = 0;
        var currentOffset = 0;
        var chunkIndex = 0;

        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            var sentenceWithSpace = (i < sentences.Count - 1) ? sentence + " " : sentence;

            // Check if adding this sentence would exceed max chunk size
            if (currentChunk.Length > 0 && currentChunk.Length + sentenceWithSpace.Length > _options.MaxChunkSize)
            {
                // Create chunk from accumulated sentences
                var chunkText = currentChunk.ToString().TrimEnd();
                chunks.Add(new TextChunk
                {
                    Text = chunkText,
                    Index = chunkIndex++,
                    StartOffset = chunkStartOffset,
                    EndOffset = chunkStartOffset + chunkText.Length
                });

                // Handle overlap - include last few sentences in next chunk
                if (_options.ChunkOverlap > 0)
                {
                    var overlapText = GetOverlapText(currentChunk.ToString(), _options.ChunkOverlap);
                    currentChunk.Clear();
                    currentChunk.Append(overlapText);
                    chunkStartOffset = chunkStartOffset + chunkText.Length - overlapText.Length;
                }
                else
                {
                    currentChunk.Clear();
                    chunkStartOffset = currentOffset;
                }
            }

            currentChunk.Append(sentenceWithSpace);
            currentOffset += sentenceWithSpace.Length;
        }

        // Add remaining text as final chunk
        if (currentChunk.Length > 0)
        {
            var chunkText = currentChunk.ToString().TrimEnd();
            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = chunkIndex,
                StartOffset = chunkStartOffset,
                EndOffset = chunkStartOffset + chunkText.Length
            });
        }

        return chunks;
    }

    private List<TextChunk> ChunkByParagraph(string text)
    {
        var paragraphs = ParagraphPattern.Split(text)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        var chunks = new List<TextChunk>();
        var currentChunk = new StringBuilder();
        var chunkStartOffset = 0;
        var currentOffset = 0;
        var chunkIndex = 0;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var paragraphWithNewlines = (i < paragraphs.Count - 1) ? paragraph + "\n\n" : paragraph;

            // Check if adding this paragraph would exceed max chunk size
            if (currentChunk.Length > 0 && currentChunk.Length + paragraphWithNewlines.Length > _options.MaxChunkSize)
            {
                // Create chunk from accumulated paragraphs
                var chunkText = currentChunk.ToString().TrimEnd();
                chunks.Add(new TextChunk
                {
                    Text = chunkText,
                    Index = chunkIndex++,
                    StartOffset = chunkStartOffset,
                    EndOffset = chunkStartOffset + chunkText.Length
                });

                currentChunk.Clear();
                chunkStartOffset = currentOffset;
            }

            currentChunk.Append(paragraphWithNewlines);
            currentOffset += paragraphWithNewlines.Length;
        }

        // Add remaining text as final chunk
        if (currentChunk.Length > 0)
        {
            var chunkText = currentChunk.ToString().TrimEnd();
            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = chunkIndex,
                StartOffset = chunkStartOffset,
                EndOffset = chunkStartOffset + chunkText.Length
            });
        }

        return chunks;
    }

    private static string GetOverlapText(string text, int overlapSize)
    {
        if (text.Length <= overlapSize)
        {
            return text;
        }

        // Get the last 'overlapSize' characters
        var overlap = text.Substring(text.Length - overlapSize);

        // Try to start at a word boundary
        var firstSpaceIndex = overlap.IndexOf(' ');
        if (firstSpaceIndex > 0)
        {
            overlap = overlap.Substring(firstSpaceIndex + 1);
        }

        return overlap;
    }
}
