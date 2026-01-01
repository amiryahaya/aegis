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

    public Task<Result<bool>> TrackEntityAsync(
        string sessionId,
        TrackedEntity entity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = _sessions.GetOrAdd(sessionId, _ => new SessionMemory());

            // Check if entity already exists
            var existing = session.TrackedEntities.FirstOrDefault(
                e => e.Name.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastMentionedAt = DateTime.UtcNow;
                existing.MentionCount++;
            }
            else
            {
                session.TrackedEntities.Add(entity);
            }

            session.LastAccessed = DateTime.UtcNow;
            _logger.LogDebug("Tracked entity {Entity} in session {SessionId}", entity.Name, sessionId);

            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking entity in working memory");
            return Task.FromResult(Result<bool>.Failure(
                Error.Internal("WorkingMemory.TrackEntityError", ex.Message)));
        }
    }

    public Task<Result<List<TrackedEntity>>> GetTrackedEntitiesAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<List<TrackedEntity>>.Success(new List<TrackedEntity>()));
            }

            session.LastAccessed = DateTime.UtcNow;
            return Task.FromResult(Result<List<TrackedEntity>>.Success(session.TrackedEntities.ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked entities");
            return Task.FromResult(Result<List<TrackedEntity>>.Failure(
                Error.Internal("WorkingMemory.GetEntitiesError", ex.Message)));
        }
    }

    public Task<Result<TrackedEntity?>> ResolveReferenceAsync(
        string sessionId,
        string reference,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<TrackedEntity?>.Success(null));
            }

            var referenceLower = reference.ToLowerInvariant().Trim();
            TrackedEntity? resolved = null;

            // Personal pronouns refer to people
            var personPronouns = new[] { "he", "she", "him", "her", "his", "hers", "they", "them" };
            // Impersonal pronouns refer to things/organizations
            var thingPronouns = new[] { "it", "its", "this", "that" };

            if (personPronouns.Contains(referenceLower))
            {
                // Get most recently mentioned person
                resolved = session.TrackedEntities
                    .Where(e => e.Type.Equals("Person", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.LastMentionedAt)
                    .FirstOrDefault();
            }
            else if (thingPronouns.Contains(referenceLower))
            {
                // Get most recently mentioned non-person entity
                resolved = session.TrackedEntities
                    .Where(e => !e.Type.Equals("Person", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.LastMentionedAt)
                    .FirstOrDefault();
            }
            else
            {
                // Try to match by name or alias
                resolved = session.TrackedEntities.FirstOrDefault(e =>
                    e.Name.Equals(reference, StringComparison.OrdinalIgnoreCase) ||
                    e.Aliases.Any(a => a.Equals(reference, StringComparison.OrdinalIgnoreCase)));
            }

            session.LastAccessed = DateTime.UtcNow;
            return Task.FromResult(Result<TrackedEntity?>.Success(resolved));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving reference");
            return Task.FromResult(Result<TrackedEntity?>.Failure(
                Error.Internal("WorkingMemory.ResolveReferenceError", ex.Message)));
        }
    }

    public Task<Result<bool>> UpdateEntityMentionAsync(
        string sessionId,
        string entityName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<bool>.Success(false));
            }

            var entity = session.TrackedEntities.FirstOrDefault(
                e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

            if (entity != null)
            {
                entity.LastMentionedAt = DateTime.UtcNow;
                entity.MentionCount++;
            }

            session.LastAccessed = DateTime.UtcNow;
            return Task.FromResult(Result<bool>.Success(entity != null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating entity mention");
            return Task.FromResult(Result<bool>.Failure(
                Error.Internal("WorkingMemory.UpdateMentionError", ex.Message)));
        }
    }

    public Task<Result<bool>> SetCurrentTopicAsync(
        string sessionId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = _sessions.GetOrAdd(sessionId, _ => new SessionMemory());
            session.CurrentTopic = topic;
            session.LastAccessed = DateTime.UtcNow;

            _logger.LogDebug("Set topic '{Topic}' for session {SessionId}", topic, sessionId);
            return Task.FromResult(Result<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting current topic");
            return Task.FromResult(Result<bool>.Failure(
                Error.Internal("WorkingMemory.SetTopicError", ex.Message)));
        }
    }

    public Task<Result<string?>> GetCurrentTopicAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<string?>.Success(null));
            }

            session.LastAccessed = DateTime.UtcNow;
            return Task.FromResult(Result<string?>.Success(session.CurrentTopic));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current topic");
            return Task.FromResult(Result<string?>.Failure(
                Error.Internal("WorkingMemory.GetTopicError", ex.Message)));
        }
    }

    public Task<Result<ConversationSummary>> GetConversationSummaryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<ConversationSummary>.Success(new ConversationSummary()));
            }

            var history = session.History;
            var summary = new ConversationSummary
            {
                TotalMessages = history.Count,
                UserMessages = history.Count(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase)),
                AssistantMessages = history.Count(m => m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)),
                SystemMessages = history.Count(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)),
                CurrentTopic = session.CurrentTopic,
                TrackedEntityNames = session.TrackedEntities.Select(e => e.Name).ToList(),
                StartedAt = history.FirstOrDefault()?.Timestamp,
                LastMessageAt = history.LastOrDefault()?.Timestamp,
                EstimatedTokenCount = EstimateTokenCount(history)
            };

            session.LastAccessed = DateTime.UtcNow;
            return Task.FromResult(Result<ConversationSummary>.Success(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation summary");
            return Task.FromResult(Result<ConversationSummary>.Failure(
                Error.Internal("WorkingMemory.GetSummaryError", ex.Message)));
        }
    }

    public Task<Result<string>> GetContextWindowAsync(
        string sessionId,
        int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(Result<string>.Success(""));
            }

            var sb = new System.Text.StringBuilder();

            // Add current topic if set
            if (!string.IsNullOrEmpty(session.CurrentTopic))
            {
                sb.AppendLine($"Current Topic: {session.CurrentTopic}");
                sb.AppendLine();
            }

            // Add tracked entities summary
            if (session.TrackedEntities.Count > 0)
            {
                sb.AppendLine("Entities in conversation:");
                foreach (var entity in session.TrackedEntities.Take(5))
                {
                    sb.AppendLine($"- {entity.Name} ({entity.Type})");
                }
                sb.AppendLine();
            }

            // Add recent conversation history
            sb.AppendLine("Conversation History:");
            var recentMessages = session.History
                .OrderByDescending(m => m.Timestamp)
                .Take(maxMessages)
                .Reverse()
                .ToList();

            foreach (var message in recentMessages)
            {
                var role = message.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? "User"
                    : "Assistant";
                sb.AppendLine($"{role}: {message.Content}");
            }

            session.LastAccessed = DateTime.UtcNow;
            return Task.FromResult(Result<string>.Success(sb.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context window");
            return Task.FromResult(Result<string>.Failure(
                Error.Internal("WorkingMemory.GetContextWindowError", ex.Message)));
        }
    }

    private int EstimateTokenCount(List<ConversationMessage> messages)
    {
        // Rough estimation: ~4 characters per token
        var totalChars = messages.Sum(m => m.Content.Length);
        return totalChars / 4;
    }

    private class SessionMemory
    {
        public ConcurrentDictionary<string, object> Data { get; } = new();
        public List<ConversationMessage> History { get; } = new();
        public List<TrackedEntity> TrackedEntities { get; } = new();
        public string? CurrentTopic { get; set; }
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
        public DateTime? Expiration { get; set; }
    }
}
