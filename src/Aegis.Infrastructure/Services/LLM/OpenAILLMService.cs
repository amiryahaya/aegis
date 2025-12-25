using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Aegis.Infrastructure.Services.LLM;

public class OpenAILLMService : ILLMService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAILLMService> _logger;
    private readonly string _model;

    public OpenAILLMService(
        string apiKey,
        ILogger<OpenAILLMService> logger,
        string model = "gpt-4o-mini")
    {
        _chatClient = new ChatClient(model, apiKey);
        _logger = logger;
        _model = model;
    }

    public async Task<Result<string>> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

            var content = response.Value.Content[0].Text;
            return Result<string>.Success(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM response");
            return Result<string>.Failure(Error.Failure("LLM.Error", ex.Message));
        }
    }

    public async Task<Result<RAGResponse>> GenerateRAGResponseAsync(
        string query,
        IEnumerable<string> contexts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contextList = contexts.ToList();

            // Build prompt with numbered contexts
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are a helpful AI assistant. Answer the user's question based on the provided context.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Context:");

            for (int i = 0; i < contextList.Count; i++)
            {
                promptBuilder.AppendLine($"[{i + 1}] {contextList[i]}");
                promptBuilder.AppendLine();
            }

            promptBuilder.AppendLine("Instructions:");
            promptBuilder.AppendLine("- Answer the question using ONLY the information from the context above.");
            promptBuilder.AppendLine("- Cite your sources using [1], [2], etc. when referencing context.");
            promptBuilder.AppendLine("- If the context doesn't contain enough information, say so.");
            promptBuilder.AppendLine("- Be concise but complete.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine($"Question: {query}");

            var messages = new List<ChatMessage>
            {
                new UserChatMessage(promptBuilder.ToString())
            };

            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            var content = response.Value.Content[0].Text;

            // Extract citations from the response
            var citations = ExtractCitations(content, contextList);

            return Result<RAGResponse>.Success(new RAGResponse
            {
                Response = content,
                Citations = citations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response");
            return Result<RAGResponse>.Failure(Error.Failure("LLM.RAGError", ex.Message));
        }
    }

    public async IAsyncEnumerable<Result<string>> GenerateStreamingResponseAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AsyncResultCollection<StreamingChatCompletionUpdate>? streamingUpdates = null;

        try
        {
            var messages = new List<ChatMessage>
            {
                new UserChatMessage(prompt)
            };

            streamingUpdates = _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);

            await foreach (var update in streamingUpdates.WithCancellation(cancellationToken))
            {
                foreach (var contentPart in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentPart.Text))
                    {
                        yield return Result<string>.Success(contentPart.Text);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming LLM response");
            yield return Result<string>.Failure(Error.Failure("LLM.StreamingError", ex.Message));
        }
    }

    private static List<Citation> ExtractCitations(string response, List<string> contexts)
    {
        var citations = new List<Citation>();

        // Find all citation markers like [1], [2], etc.
        var citationPattern = @"\[(\d+)\]";
        var matches = Regex.Matches(response, citationPattern);

        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int index))
            {
                // Convert to 0-based index
                var contextIndex = index - 1;

                if (contextIndex >= 0 && contextIndex < contexts.Count)
                {
                    // Extract relevant snippet from the context (first 200 chars)
                    var text = contexts[contextIndex];
                    if (text.Length > 200)
                    {
                        text = text.Substring(0, 200) + "...";
                    }

                    // Avoid duplicate citations
                    if (!citations.Any(c => c.Index == contextIndex))
                    {
                        citations.Add(new Citation
                        {
                            Index = contextIndex,
                            Text = text
                        });
                    }
                }
            }
        }

        return citations;
    }
}
