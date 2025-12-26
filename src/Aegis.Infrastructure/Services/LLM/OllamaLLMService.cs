using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.LLM;

/// <summary>
/// LLM service implementation using Ollama for local inference
/// </summary>
public class OllamaLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLMService> _logger;
    private readonly string _model;
    private readonly string _baseUrl;

    public OllamaLLMService(
        HttpClient httpClient,
        ILogger<OllamaLLMService> logger,
        string baseUrl = "http://localhost:11434",
        string model = "qwen2.5:latest")
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = baseUrl;
        _model = model;
    }

    public async Task<Result<string>> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = false
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/generate",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                cancellationToken: cancellationToken);

            if (result?.Response == null)
            {
                return Result<string>.Failure(Error.Internal("Ollama.EmptyResponse", "Ollama returned empty response"));
            }

            return Result<string>.Success(result.Response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama HTTP request failed");
            return Result<string>.Failure(Error.Internal("Ollama.RequestFailed", $"Ollama request failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM response with Ollama");
            return Result<string>.Failure(Error.Internal("LLM.Error", ex.Message));
        }
    }

    public async Task<Result<RAGResponse>> GenerateRAGResponseAsync(
        string query,
        IEnumerable<string> contexts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build context from retrieved passages
            var contextList = contexts.ToList();
            var context = string.Join("\n\n", contextList.Select((ctx, idx) =>
                $"[{idx + 1}] {ctx}"));

            // Build prompt
            var prompt = BuildRAGPrompt(query, context);

            // Generate response
            var responseResult = await GenerateResponseAsync(prompt, cancellationToken);

            return responseResult.Match(
                response => Result<RAGResponse>.Success(new RAGResponse
                {
                    Response = response,
                    Citations = ExtractCitations(response, contextList)
                }),
                error => Result<RAGResponse>.Failure(error));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response with Ollama");
            return Result<RAGResponse>.Failure(Error.Internal("LLM.RAGError", ex.Message));
        }
    }

    public async IAsyncEnumerable<Result<string>> GenerateStreamingResponseAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate");

        // Initialize connection - handle errors without yielding in catch
        Result<StreamReader>? connectionResult = null;
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = true
            };

            httpRequest.Content = JsonContent.Create(request);

            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            reader = new StreamReader(stream);
            connectionResult = Result<StreamReader>.Success(reader);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama streaming request failed");
            connectionResult = Result<StreamReader>.Failure(Error.Internal("Ollama.StreamFailed", $"Streaming request failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Ollama streaming response");
            connectionResult = Result<StreamReader>.Failure(Error.Internal("LLM.StreamError", ex.Message));
        }

        // If connection failed, yield error and cleanup
        if (connectionResult.IsFailure)
        {
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            httpRequest.Dispose();
            yield return Result<string>.Failure(connectionResult.Error!);
            yield break;
        }

        // Stream results
        try
        {
            while (!reader!.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                OllamaGenerateResponse? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Ollama streaming response: {Line}", line);
                    continue; // Skip malformed chunks
                }

                if (chunk?.Response != null)
                {
                    yield return Result<string>.Success(chunk.Response);
                }

                if (chunk?.Done == true)
                {
                    break;
                }
            }
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
            httpRequest.Dispose();
        }
    }

    private string BuildRAGPrompt(string query, string context)
    {
        return $"""
            You are a helpful AI assistant. Answer the user's question based on the provided context.
            When citing information, reference the context using [1], [2], etc.

            Context:
            {context}

            Question: {query}

            Answer:
            """;
    }

    private List<Citation> ExtractCitations(string response, List<string> contexts)
    {
        var citations = new List<Citation>();

        // Simple citation extraction: look for [1], [2], etc. in the response
        for (int i = 0; i < contexts.Count; i++)
        {
            var citationPattern = $"[{i + 1}]";
            if (response.Contains(citationPattern))
            {
                citations.Add(new Citation
                {
                    Index = i,
                    Text = contexts[i].Length > 100 ? contexts[i].Substring(0, 100) + "..." : contexts[i]
                });
            }
        }

        return citations;
    }
}

// Ollama API DTOs
internal record OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("options")]
    public Dictionary<string, object>? Options { get; init; }
}

internal record OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("response")]
    public string? Response { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }

    [JsonPropertyName("context")]
    public int[]? Context { get; init; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; init; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; init; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; init; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; init; }
}
