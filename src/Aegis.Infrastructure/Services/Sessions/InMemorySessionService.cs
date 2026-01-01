using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Sessions;

public class InMemorySessionService : ISessionService
{
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _userSessions = new();
    private readonly ConcurrentDictionary<Guid, SessionShare> _shares = new();
    private readonly ILogger<InMemorySessionService> _logger;

    public InMemorySessionService(ILogger<InMemorySessionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<Session>> CreateAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = new Session
            {
                UserId = request.UserId,
                TeamId = request.TeamId,
                WorkspaceId = request.WorkspaceId,
                Title = request.Title ?? $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                Description = request.Description,
                Type = request.Type,
                Tags = request.Tags?.ToList() ?? new List<string>(),
                Settings = request.Settings ?? new SessionSettings(),
                Metadata = request.Metadata?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, object>(),
                LastActivityAt = DateTime.UtcNow
            };

            _sessions[session.Id] = session;

            _userSessions.AddOrUpdate(
                request.UserId,
                _ => new List<Guid> { session.Id },
                (_, list) =>
                {
                    list.Add(session.Id);
                    return list;
                });

            _logger.LogInformation(
                "Created session {SessionId} for user {UserId}",
                session.Id, request.UserId);

            return Task.FromResult(Result<Session>.Success(session));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {UserId}", request.UserId);
            return Task.FromResult(Result<Session>.Failure(
                Error.Internal("Session.CreateFailed", "Failed to create session")));
        }
    }

    public Task<Result<Session>> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<Session>.Success(session));
        }

        return Task.FromResult(Result<Session>.Failure(
            Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
    }

    public Task<Result<SessionPage>> GetForUserAsync(
        Guid userId,
        SessionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SessionFilter();

        if (!_userSessions.TryGetValue(userId, out var sessionIds))
        {
            return Task.FromResult(Result<SessionPage>.Success(new SessionPage
            {
                Items = Array.Empty<Session>(),
                TotalCount = 0,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            }));
        }

        var sessions = sessionIds
            .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
            .Where(s => s != null)
            .Cast<Session>();

        sessions = ApplyFilter(sessions, filter);
        sessions = ApplySort(sessions, filter);

        var totalCount = sessions.Count();
        var pagedItems = sessions
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return Task.FromResult(Result<SessionPage>.Success(new SessionPage
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        }));
    }

    public Task<Result<IReadOnlyList<Session>>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_userSessions.TryGetValue(userId, out var sessionIds))
        {
            return Task.FromResult(Result<IReadOnlyList<Session>>.Success(Array.Empty<Session>()));
        }

        var activeSessions = sessionIds
            .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
            .Where(s => s != null && s.Status == SessionStatus.Active)
            .Cast<Session>()
            .OrderByDescending(s => s.LastActivityAt)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<Session>>.Success(activeSessions));
    }

    public Task<Result<SessionTurn>> AddTurnAsync(
        Guid sessionId,
        AddTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<SessionTurn>.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        if (session.Status != SessionStatus.Active)
        {
            return Task.FromResult(Result<SessionTurn>.Failure(
                Error.Validation("Session.NotActive", "Cannot add turns to an inactive session")));
        }

        if (session.Turns.Count >= session.Settings.MaxTurns)
        {
            return Task.FromResult(Result<SessionTurn>.Failure(
                Error.Validation("Session.MaxTurnsReached", "Session has reached maximum number of turns")));
        }

        var turn = new SessionTurn
        {
            SessionId = sessionId,
            TurnNumber = session.Turns.Count + 1,
            UserQuery = request.Query,
            Context = request.Context?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, object>(),
            Status = TurnStatus.Pending
        };

        var updatedTurns = session.Turns.ToList();
        updatedTurns.Add(turn);

        var updatedSession = session with
        {
            Turns = updatedTurns,
            LastActivityAt = DateTime.UtcNow,
            // Auto-generate title from first query if still default
            Title = session.Turns.Count == 0 && session.Title.StartsWith("Session ")
                ? GenerateTitleFromQuery(request.Query)
                : session.Title
        };

        _sessions[sessionId] = updatedSession;

        _logger.LogDebug(
            "Added turn {TurnNumber} to session {SessionId}",
            turn.TurnNumber, sessionId);

        return Task.FromResult(Result<SessionTurn>.Success(turn));
    }

    public Task<Result<IReadOnlyList<SessionTurn>>> GetConversationAsync(
        Guid sessionId,
        int? lastNTurns = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<IReadOnlyList<SessionTurn>>.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        IEnumerable<SessionTurn> turns = session.Turns;
        if (lastNTurns.HasValue && lastNTurns.Value > 0)
        {
            turns = turns.TakeLast(lastNTurns.Value);
        }

        return Task.FromResult(Result<IReadOnlyList<SessionTurn>>.Success(turns.ToList()));
    }

    public Task<Result<Session>> UpdateAsync(
        Guid sessionId,
        UpdateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<Session>.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        var updated = session with
        {
            Title = request.Title ?? session.Title,
            Description = request.Description ?? session.Description,
            Tags = request.Tags?.ToList() ?? session.Tags,
            Settings = request.Settings ?? session.Settings,
            Metadata = request.Metadata?.ToDictionary(k => k.Key, v => v.Value) ?? session.Metadata,
            LastActivityAt = DateTime.UtcNow
        };

        _sessions[sessionId] = updated;

        _logger.LogDebug("Updated session {SessionId}", sessionId);

        return Task.FromResult(Result<Session>.Success(updated));
    }

    public Task<Result> UpdateTitleAsync(
        Guid sessionId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("Session.InvalidTitle", "Title cannot be empty")));
        }

        _sessions[sessionId] = session with { Title = title, LastActivityAt = DateTime.UtcNow };

        return Task.FromResult(Result.Success());
    }

    public Task<Result> EndSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        _sessions[sessionId] = session with
        {
            Status = SessionStatus.Ended,
            EndedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Ended session {SessionId}", sessionId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        // Remove from user sessions
        if (_userSessions.TryGetValue(session.UserId, out var list))
        {
            list.Remove(sessionId);
        }

        // Remove any shares
        var sharesToRemove = _shares.Values.Where(s => s.SessionId == sessionId).ToList();
        foreach (var share in sharesToRemove)
        {
            _shares.TryRemove(share.Id, out _);
        }

        _logger.LogInformation("Deleted session {SessionId}", sessionId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<int>> DeleteOlderThanAsync(
        DateTime before,
        CancellationToken cancellationToken = default)
    {
        var toDelete = _sessions.Values
            .Where(s => s.CreatedAt < before && s.Status != SessionStatus.Active)
            .ToList();

        var count = 0;
        foreach (var session in toDelete)
        {
            if (_sessions.TryRemove(session.Id, out _))
            {
                if (_userSessions.TryGetValue(session.UserId, out var list))
                {
                    list.Remove(session.Id);
                }
                count++;
            }
        }

        _logger.LogInformation("Deleted {Count} sessions older than {Before}", count, before);

        return Task.FromResult(Result<int>.Success(count));
    }

    public Task<Result<SessionStats>> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_userSessions.TryGetValue(userId, out var sessionIds))
        {
            return Task.FromResult(Result<SessionStats>.Success(new SessionStats
            {
                UserId = userId
            }));
        }

        var sessions = sessionIds
            .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
            .Where(s => s != null)
            .Cast<Session>()
            .ToList();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);

        var allTurns = sessions.SelectMany(s => s.Turns).ToList();
        var allFeedback = allTurns.Where(t => t.Feedback != null).Select(t => t.Feedback!).ToList();

        var stats = new SessionStats
        {
            UserId = userId,
            TotalSessions = sessions.Count,
            ActiveSessions = sessions.Count(s => s.Status == SessionStatus.Active),
            TotalTurns = allTurns.Count,
            TotalQueriesThisWeek = allTurns.Count(t => t.CreatedAt >= weekAgo),
            TotalQueriesThisMonth = allTurns.Count(t => t.CreatedAt >= monthAgo),
            AverageSessionDuration = sessions
                .Where(s => s.Duration.HasValue)
                .Select(s => s.Duration!.Value.TotalMinutes)
                .DefaultIfEmpty(0)
                .Average(),
            AverageTurnsPerSession = sessions.Count > 0 ? (double)allTurns.Count / sessions.Count : 0,
            SessionsByType = sessions
                .GroupBy(s => s.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            FeedbackByRating = allFeedback
                .GroupBy(f => f.Rating)
                .ToDictionary(g => g.Key, g => g.Count()),
            LastSessionAt = sessions.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.CreatedAt,
            TopTags = sessions
                .SelectMany(s => s.Tags)
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList()
        };

        return Task.FromResult(Result<SessionStats>.Success(stats));
    }

    public Task<Result<SessionShare>> ShareAsync(
        Guid sessionId,
        ShareSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<SessionShare>.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        var share = new SessionShare
        {
            SessionId = sessionId,
            SharedByUserId = session.UserId,
            SharedWithUserId = request.ShareWithUserId,
            SharedWithTeamId = request.ShareWithTeamId,
            IsPublic = request.IsPublic,
            Permission = request.Permission,
            ShareLink = request.IsPublic ? $"/shared/{Guid.NewGuid():N}" : null,
            ExpiresAt = request.ExpiresAt
        };

        _shares[share.Id] = share;

        _logger.LogInformation(
            "Shared session {SessionId} (public: {IsPublic})",
            sessionId, request.IsPublic);

        return Task.FromResult(Result<SessionShare>.Success(share));
    }

    public Task<Result<IReadOnlyList<Session>>> GetSharedWithUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sharedSessionIds = _shares.Values
            .Where(s => s.SharedWithUserId == userId &&
                       (!s.ExpiresAt.HasValue || s.ExpiresAt.Value > now))
            .Select(s => s.SessionId)
            .Distinct();

        var sessions = sharedSessionIds
            .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
            .Where(s => s != null)
            .Cast<Session>()
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<Session>>.Success(sessions));
    }

    public Task<Result<SessionExport>> ExportAsync(
        Guid sessionId,
        SessionExportFormat format,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<SessionExport>.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        var (content, contentType, extension) = format switch
        {
            SessionExportFormat.Json => (ExportToJson(session), "application/json", "json"),
            SessionExportFormat.Markdown => (ExportToMarkdown(session), "text/markdown", "md"),
            SessionExportFormat.Html => (ExportToHtml(session), "text/html", "html"),
            SessionExportFormat.Text => (ExportToText(session), "text/plain", "txt"),
            _ => (ExportToJson(session), "application/json", "json")
        };

        var export = new SessionExport
        {
            SessionId = sessionId,
            Format = format,
            Content = content,
            FileName = $"{SanitizeFileName(session.Title)}_{session.Id:N}.{extension}",
            ContentType = contentType
        };

        return Task.FromResult(Result<SessionExport>.Success(export));
    }

    public Task<Result> AddTurnFeedbackAsync(
        Guid sessionId,
        Guid turnId,
        TurnFeedback feedback,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        var turnIndex = session.Turns.FindIndex(t => t.Id == turnId);
        if (turnIndex < 0)
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Turn.NotFound", $"Turn {turnId} not found in session")));
        }

        var updatedTurns = session.Turns.ToList();
        updatedTurns[turnIndex] = updatedTurns[turnIndex] with { Feedback = feedback };

        _sessions[sessionId] = session with { Turns = updatedTurns };

        _logger.LogDebug(
            "Added {Rating} feedback to turn {TurnId} in session {SessionId}",
            feedback.Rating, turnId, sessionId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<SessionPage>> GetRecentAsync(
        SessionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SessionFilter();

        var sessions = _sessions.Values.AsEnumerable();
        sessions = ApplyFilter(sessions, filter);
        sessions = ApplySort(sessions, filter);

        var totalCount = sessions.Count();
        var pagedItems = sessions
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return Task.FromResult(Result<SessionPage>.Success(new SessionPage
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        }));
    }

    public Task<Result<SessionPage>> SearchAsync(
        string query,
        Guid? userId = null,
        SessionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SessionFilter();
        var searchTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var sessions = _sessions.Values.AsEnumerable();

        if (userId.HasValue)
        {
            if (!_userSessions.TryGetValue(userId.Value, out var userSessionIds))
            {
                return Task.FromResult(Result<SessionPage>.Success(new SessionPage
                {
                    Items = Array.Empty<Session>(),
                    TotalCount = 0,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize
                }));
            }
            sessions = sessions.Where(s => userSessionIds.Contains(s.Id));
        }

        // Search in title, description, tags, and conversation content
        sessions = sessions.Where(s =>
            searchTerms.All(term =>
                s.Title.ToLowerInvariant().Contains(term) ||
                (s.Description?.ToLowerInvariant().Contains(term) ?? false) ||
                s.Tags.Any(t => t.ToLowerInvariant().Contains(term)) ||
                s.Turns.Any(t =>
                    t.UserQuery.ToLowerInvariant().Contains(term) ||
                    (t.SystemResponse?.ToLowerInvariant().Contains(term) ?? false))));

        sessions = ApplyFilter(sessions, filter);
        sessions = ApplySort(sessions, filter);

        var totalCount = sessions.Count();
        var pagedItems = sessions
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return Task.FromResult(Result<SessionPage>.Success(new SessionPage
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        }));
    }

    // Helper method to complete a turn with response
    public Task<Result<SessionTurn>> CompleteTurnAsync(
        Guid sessionId,
        Guid turnId,
        string response,
        List<SessionSourceReference>? sources = null,
        List<string>? followUps = null,
        TurnMetrics? metrics = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<SessionTurn>.Failure(
                Error.NotFound("Session.NotFound", $"Session {sessionId} not found")));
        }

        var turnIndex = session.Turns.FindIndex(t => t.Id == turnId);
        if (turnIndex < 0)
        {
            return Task.FromResult(Result<SessionTurn>.Failure(
                Error.NotFound("Turn.NotFound", $"Turn {turnId} not found in session")));
        }

        var turn = session.Turns[turnIndex];
        var completedTurn = turn with
        {
            SystemResponse = response,
            Status = TurnStatus.Completed,
            Sources = sources ?? new List<SessionSourceReference>(),
            FollowUpQuestions = followUps ?? new List<string>(),
            Metrics = metrics,
            CompletedAt = DateTime.UtcNow
        };

        var updatedTurns = session.Turns.ToList();
        updatedTurns[turnIndex] = completedTurn;

        _sessions[sessionId] = session with
        {
            Turns = updatedTurns,
            LastActivityAt = DateTime.UtcNow
        };

        return Task.FromResult(Result<SessionTurn>.Success(completedTurn));
    }

    #region Private Helpers

    private static IEnumerable<Session> ApplyFilter(IEnumerable<Session> sessions, SessionFilter filter)
    {
        if (filter.Status.HasValue)
        {
            sessions = sessions.Where(s => s.Status == filter.Status.Value);
        }

        if (filter.Type.HasValue)
        {
            sessions = sessions.Where(s => s.Type == filter.Type.Value);
        }

        if (filter.Tags?.Any() == true)
        {
            sessions = sessions.Where(s => filter.Tags.Any(t => s.Tags.Contains(t)));
        }

        if (filter.Since.HasValue)
        {
            sessions = sessions.Where(s => s.CreatedAt >= filter.Since.Value);
        }

        if (filter.Until.HasValue)
        {
            sessions = sessions.Where(s => s.CreatedAt <= filter.Until.Value);
        }

        if (filter.TeamId.HasValue)
        {
            sessions = sessions.Where(s => s.TeamId == filter.TeamId.Value);
        }

        if (filter.WorkspaceId.HasValue)
        {
            sessions = sessions.Where(s => s.WorkspaceId == filter.WorkspaceId.Value);
        }

        return sessions;
    }

    private static IEnumerable<Session> ApplySort(IEnumerable<Session> sessions, SessionFilter filter)
    {
        sessions = filter.SortBy switch
        {
            SessionSortBy.CreatedAt => filter.SortDescending
                ? sessions.OrderByDescending(s => s.CreatedAt)
                : sessions.OrderBy(s => s.CreatedAt),
            SessionSortBy.LastActivity => filter.SortDescending
                ? sessions.OrderByDescending(s => s.LastActivityAt ?? s.CreatedAt)
                : sessions.OrderBy(s => s.LastActivityAt ?? s.CreatedAt),
            SessionSortBy.TurnCount => filter.SortDescending
                ? sessions.OrderByDescending(s => s.TurnCount)
                : sessions.OrderBy(s => s.TurnCount),
            SessionSortBy.Title => filter.SortDescending
                ? sessions.OrderByDescending(s => s.Title)
                : sessions.OrderBy(s => s.Title),
            _ => sessions.OrderByDescending(s => s.LastActivityAt ?? s.CreatedAt)
        };

        return sessions;
    }

    private static string GenerateTitleFromQuery(string query)
    {
        // Take first 50 chars and clean up
        var title = query.Length > 50 ? query[..50] + "..." : query;
        title = title.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return title;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ExportToJson(Session session)
    {
        return JsonSerializer.Serialize(session, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string ExportToMarkdown(Session session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(session.Description))
        {
            sb.AppendLine($"*{session.Description}*");
            sb.AppendLine();
        }

        sb.AppendLine($"**Type:** {session.Type}");
        sb.AppendLine($"**Created:** {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        if (session.Tags.Any())
        {
            sb.AppendLine($"**Tags:** {string.Join(", ", session.Tags)}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var turn in session.Turns)
        {
            sb.AppendLine($"## Turn {turn.TurnNumber}");
            sb.AppendLine();
            sb.AppendLine($"**User:** {turn.UserQuery}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(turn.SystemResponse))
            {
                sb.AppendLine($"**Assistant:** {turn.SystemResponse}");
                sb.AppendLine();
            }

            if (turn.Sources.Any())
            {
                sb.AppendLine("**Sources:**");
                foreach (var source in turn.Sources)
                {
                    sb.AppendLine($"- {source.DocumentName} (relevance: {source.RelevanceScore:P0})");
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExportToHtml(Session session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine($"<title>{session.Title}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine(".user { background: #e3f2fd; padding: 10px; border-radius: 8px; margin: 10px 0; }");
        sb.AppendLine(".assistant { background: #f5f5f5; padding: 10px; border-radius: 8px; margin: 10px 0; }");
        sb.AppendLine(".sources { font-size: 0.9em; color: #666; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{session.Title}</h1>");

        foreach (var turn in session.Turns)
        {
            sb.AppendLine($"<div class=\"user\"><strong>User:</strong> {turn.UserQuery}</div>");
            if (!string.IsNullOrEmpty(turn.SystemResponse))
            {
                sb.AppendLine($"<div class=\"assistant\"><strong>Assistant:</strong> {turn.SystemResponse}</div>");
            }
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string ExportToText(Session session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(session.Title);
        sb.AppendLine(new string('=', session.Title.Length));
        sb.AppendLine();

        foreach (var turn in session.Turns)
        {
            sb.AppendLine($"User: {turn.UserQuery}");
            if (!string.IsNullOrEmpty(turn.SystemResponse))
            {
                sb.AppendLine($"Assistant: {turn.SystemResponse}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion
}
