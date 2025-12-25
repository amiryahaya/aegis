using Aegis.Domain.Common;
using Aegis.Domain.Services;
using System.Runtime.CompilerServices;

namespace Aegis.Infrastructure.Services.LLM;

/// <summary>
/// Mock LLM service for testing purposes.
/// Returns predefined responses without calling actual LLM APIs.
/// For production use, replace with SemanticKernelLLMService.
/// </summary>
public class MockLLMService : ILLMService
{
    public Task<Result<string>> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(Result<string>.Failure(
                Error.Validation("LLM.EmptyPrompt", "Prompt cannot be empty")));
        }

        // Generate a simple mock response
        var response = $"Mock response to: {prompt.Substring(0, Math.Min(50, prompt.Length))}...";
        return Task.FromResult(Result<string>.Success(response));
    }

    public Task<Result<RAGResponse>> GenerateRAGResponseAsync(
        string query,
        IEnumerable<string> contexts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(Result<RAGResponse>.Failure(
                Error.Validation("LLM.EmptyQuery", "Query cannot be empty")));
        }

        var contextList = contexts.ToList();

        if (contextList.Count == 0)
        {
            return Task.FromResult(Result<RAGResponse>.Failure(
                Error.Validation("LLM.EmptyContext", "Context cannot be empty")));
        }

        // Generate citations for all contexts
        var citations = contextList
            .Select((text, index) => new Citation
            {
                Index = index,
                Text = text
            })
            .ToList();

        // Generate a mock response that references the contexts
        var response = $"Based on the provided information, here's what I found about '{query}': " +
                      $"The contexts mention {contextList.Count} relevant points. " +
                      $"[Citations: {string.Join(", ", Enumerable.Range(0, contextList.Count))}]";

        var ragResponse = new RAGResponse
        {
            Response = response,
            Citations = citations
        };

        return Task.FromResult(Result<RAGResponse>.Success(ragResponse));
    }

    public async IAsyncEnumerable<Result<string>> GenerateStreamingResponseAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield return Result<string>.Failure(
                Error.Validation("LLM.EmptyPrompt", "Prompt cannot be empty"));
            yield break;
        }

        // Simulate streaming by yielding tokens one at a time
        var tokens = new[] { "Mock", " streaming", " response", " for", " prompt", "." };

        foreach (var token in tokens)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            await Task.Delay(10, cancellationToken); // Simulate network delay
            yield return Result<string>.Success(token);
        }
    }
}
