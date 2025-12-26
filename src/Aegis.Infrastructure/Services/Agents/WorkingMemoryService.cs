using System.Collections.Concurrent;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// In-memory working memory service for maintaining conversation context
/// </summary>
public class WorkingMemoryService : IWorkingMemory
{
    private readonly ConcurrentDictionary<string, SessionMemory> _sessions;
    private readonly ILogger<WorkingMemoryService> _logger;

    public WorkingMemoryService(ILogger<WorkingMemoryService> logger)
    {
        _sessions = new ConcurrentDictionary<string, SessionMemory>();
        _logger = logger;
    }

    public Task<Result<bool>> SetAsync(
        string sessionId,
        string key,
        object value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = _sessions.GetOrAdd(sessionId, _ => new SessionMemory());

            session.Data[key] = value;
            session.LastAccessed = DateTime.UtcNow;

            if (expiration.HasValue)
            {
                session.Expiration = DateTime.UtcNow.Add(expiration.Value);
            }

            _logger.LogDebug("Set value for session {SessionId}, key {Key}", sessionId, key);

            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in working memory");
            return Task.FromResult(Result<bool>.Failure(
                Error.Internal("WorkingMemory.SetError", ex.Message)));
        }
    }

    public Task<Result<T?>> GetAsync<T>(
        string sessionId,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<T?>.Success(default(T)));
            }

            // Check expiration
            if (session.Expiration.HasValue && session.Expiration.Value < DateTime.UtcNow)
            {
                _sessions.TryRemove(sessionId, out _);
                return Task.FromResult(Result<T?>.Success(default(T)));
            }

            if (!session.Data.TryGetValue(key, out var value))
            {
                return Task.FromResult(Result<T?>.Success(default(T)));
            }

            session.LastAccessed = DateTime.UtcNow;

            // Convert value to T
            if (value is T typedValue)
            {
                return Task.FromResult(Result<T?>.Success(typedValue));
            }

            // Try JSON conversion for complex types
            if (value is JsonElement jsonElement)
            {
                var deserializedValue = jsonElement.Deserialize<T>();
                return Task.FromResult(Result<T?>.Success(deserializedValue));
            }

            return Task.FromResult(Result<T?>.Success(default(T)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from working memory");
            return Task.FromResult(Result<T?>.Failure(
                Error.Internal("WorkingMemory.GetError", ex.Message)));
        }
    }

    public Task<Result<Dictionary<string, object>>> GetContextAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<Dictionary<string, object>>.Success(
                    new Dictionary<string, object>()));
            }

            // Check expiration
            if (session.Expiration.HasValue && session.Expiration.Value < DateTime.UtcNow)
            {
                _sessions.TryRemove(sessionId, out _);
                return Task.FromResult(Result<Dictionary<string, object>>.Success(
                    new Dictionary<string, object>()));
            }

            session.LastAccessed = DateTime.UtcNow;

            return Task.FromResult(Result<Dictionary<string, object>>.Success(
                new Dictionary<string, object>(session.Data)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context from working memory");
            return Task.FromResult(Result<Dictionary<string, object>>.Failure(
                Error.Internal("WorkingMemory.GetContextError", ex.Message)));
        }
    }

    public Task<Result<bool>> ClearAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _sessions.TryRemove(sessionId, out _);
            _logger.LogDebug("Cleared session {SessionId}", sessionId);
            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing working memory");
            return Task.FromResult(Result<bool>.Failure(
                Error.Internal("WorkingMemory.ClearError", ex.Message)));
        }
    }

    public Task<Result<bool>> AddMessageAsync(
        string sessionId,
        ConversationMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = _sessions.GetOrAdd(sessionId, _ => new SessionMemory());

            session.History.Add(message);
            session.LastAccessed = DateTime.UtcNow;

            _logger.LogDebug("Added message to session {SessionId}", sessionId);

            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message to working memory");
            return Task.FromResult(Result<bool>.Failure(
                Error.Internal("WorkingMemory.AddMessageError", ex.Message)));
        }
    }

    public Task<Result<List<ConversationMessage>>> GetHistoryAsync(
        string sessionId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<List<ConversationMessage>>.Success(
                    new List<ConversationMessage>()));
            }

            session.LastAccessed = DateTime.UtcNow;

            var history = session.History
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .Reverse()
                .ToList();

            return Task.FromResult(Result<List<ConversationMessage>>.Success(history));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history from working memory");
            return Task.FromResult(Result<List<ConversationMessage>>.Failure(
                Error.Internal("WorkingMemory.GetHistoryError", ex.Message)));
        }
    }

    private class SessionMemory
    {
        public ConcurrentDictionary<string, object> Data { get; } = new();
        public List<ConversationMessage> History { get; } = new();
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
        public DateTime? Expiration { get; set; }
    }
}
