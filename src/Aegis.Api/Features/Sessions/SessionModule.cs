using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Sessions;

public class SessionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Sessions");

        group.MapPost("/", CreateSession)
            .WithSummary("Create a new session");

        group.MapGet("/{id:guid}", GetSession)
            .WithSummary("Get a session by ID");

        group.MapGet("/", GetUserSessions)
            .WithSummary("Get sessions for a user");

        group.MapGet("/active", GetActiveSessions)
            .WithSummary("Get active sessions for a user");

        group.MapPut("/{id:guid}", UpdateSession)
            .WithSummary("Update session metadata");

        group.MapPut("/{id:guid}/title", UpdateSessionTitle)
            .WithSummary("Update session title");

        group.MapPost("/{id:guid}/turns", AddTurn)
            .WithSummary("Add a conversation turn");

        group.MapPut("/{id:guid}/turns/{turnId:guid}/complete", CompleteTurn)
            .WithSummary("Complete a turn with response");

        group.MapGet("/{id:guid}/conversation", GetConversation)
            .WithSummary("Get conversation history");

        group.MapPost("/{id:guid}/turns/{turnId:guid}/feedback", AddTurnFeedback)
            .WithSummary("Add feedback to a turn");

        group.MapPut("/{id:guid}/end", EndSession)
            .WithSummary("End/close a session");

        group.MapDelete("/{id:guid}", DeleteSession)
            .WithSummary("Delete a session");

        group.MapGet("/stats", GetSessionStats)
            .WithSummary("Get session statistics for a user");

        group.MapPost("/{id:guid}/share", ShareSession)
            .WithSummary("Share a session");

        group.MapGet("/shared", GetSharedSessions)
            .WithSummary("Get sessions shared with user");

        group.MapGet("/{id:guid}/export", ExportSession)
            .WithSummary("Export session conversation");

        group.MapGet("/search", SearchSessions)
            .WithSummary("Search sessions by content");

        group.MapGet("/recent", GetRecentSessions)
            .WithSummary("Get recent sessions (admin)");

        // Context endpoints
        group.MapGet("/{id:guid}/context", BuildContext)
            .WithSummary("Build conversation context for next query");

        group.MapPost("/{id:guid}/context/rewrite", RewriteQuery)
            .WithSummary("Rewrite query with context");

        group.MapGet("/{id:guid}/topic", GetCurrentTopic)
            .WithSummary("Get current conversation topic");

        group.MapPost("/{id:guid}/topic-shift", DetectTopicShift)
            .WithSummary("Detect if query shifts topic");

        group.MapGet("/{id:guid}/summary", GetConversationSummary)
            .WithSummary("Get conversation summary");

        group.MapPost("/{id:guid}/follow-ups", GenerateFollowUps)
            .WithSummary("Generate follow-up questions");

        group.MapGet("/templates", GetSessionTemplates)
            .WithSummary("Get available session templates");
    }

    private static async Task<Results<Created<SessionResponse>, BadRequest<ProblemDetails>>> CreateSession(
        CreateSessionApiRequest request,
        ISessionService sessionService)
    {
        var createRequest = new CreateSessionRequest
        {
            UserId = request.UserId,
            TeamId = request.TeamId,
            WorkspaceId = request.WorkspaceId,
            Title = request.Title,
            Description = request.Description,
            Type = request.Type ?? SessionType.Query,
            Tags = request.Tags,
            Settings = request.Settings,
            Metadata = request.Metadata
        };

        var result = await sessionService.CreateAsync(createRequest);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        var response = MapToResponse(result.Value);
        return TypedResults.Created($"/api/sessions/{response.Id}", response);
    }

    private static async Task<Results<Ok<SessionResponse>, NotFound>> GetSession(
        Guid id,
        ISessionService sessionService)
    {
        var result = await sessionService.GetByIdAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Ok<SessionPageResponse>> GetUserSessions(
        [FromQuery] Guid userId,
        [FromQuery] SessionStatus? status,
        [FromQuery] SessionType? type,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ISessionService sessionService)
    {
        var filter = new SessionFilter
        {
            Status = status,
            Type = type,
            PageNumber = page ?? 1,
            PageSize = pageSize ?? 20
        };

        var result = await sessionService.GetForUserAsync(userId, filter);

        var response = new SessionPageResponse(
            result.Value.Items.Select(MapToResponse).ToList(),
            result.Value.TotalCount,
            result.Value.PageNumber,
            result.Value.PageSize,
            result.Value.TotalPages,
            result.Value.HasNextPage,
            result.Value.HasPreviousPage);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<List<SessionResponse>>> GetActiveSessions(
        [FromQuery] Guid userId,
        ISessionService sessionService)
    {
        var result = await sessionService.GetActiveSessionsAsync(userId);
        var sessions = result.Value.Select(MapToResponse).ToList();
        return TypedResults.Ok(sessions);
    }

    private static async Task<Results<Ok<SessionResponse>, NotFound>> UpdateSession(
        Guid id,
        UpdateSessionApiRequest request,
        ISessionService sessionService)
    {
        var updateRequest = new UpdateSessionRequest
        {
            Title = request.Title,
            Description = request.Description,
            Tags = request.Tags,
            Settings = request.Settings,
            Metadata = request.Metadata
        };

        var result = await sessionService.UpdateAsync(id, updateRequest);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok, NotFound, BadRequest<ProblemDetails>>> UpdateSessionTitle(
        Guid id,
        UpdateTitleRequest request,
        ISessionService sessionService)
    {
        var result = await sessionService.UpdateTitleAsync(id, request.Title);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("NotFound"))
                return TypedResults.NotFound();
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<Created<SessionTurnResponse>, NotFound, BadRequest<ProblemDetails>>> AddTurn(
        Guid id,
        AddTurnApiRequest request,
        ISessionService sessionService)
    {
        var addRequest = new AddTurnRequest
        {
            Query = request.Query,
            Context = request.Context
        };

        var result = await sessionService.AddTurnAsync(id, addRequest);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("NotFound"))
                return TypedResults.NotFound();
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        var response = MapTurnToResponse(result.Value);
        return TypedResults.Created($"/api/sessions/{id}/turns/{response.Id}", response);
    }

    private static async Task<Results<Ok<SessionTurnResponse>, NotFound>> CompleteTurn(
        Guid id,
        Guid turnId,
        CompleteTurnRequest request,
        ISessionService sessionService)
    {
        // Cast to InMemorySessionService to access CompleteTurnAsync
        if (sessionService is Infrastructure.Services.Sessions.InMemorySessionService inMemoryService)
        {
            var result = await inMemoryService.CompleteTurnAsync(
                id, turnId, request.Response, request.Sources, request.FollowUps, request.Metrics);

            if (result.IsFailure)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(MapTurnToResponse(result.Value));
        }

        return TypedResults.NotFound();
    }

    private static async Task<Results<Ok<List<SessionTurnResponse>>, NotFound>> GetConversation(
        Guid id,
        [FromQuery] int? lastN,
        ISessionService sessionService)
    {
        var result = await sessionService.GetConversationAsync(id, lastN);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var turns = result.Value.Select(MapTurnToResponse).ToList();
        return TypedResults.Ok(turns);
    }

    private static async Task<Results<Ok, NotFound>> AddTurnFeedback(
        Guid id,
        Guid turnId,
        TurnFeedbackRequest request,
        ISessionService sessionService)
    {
        var feedback = new TurnFeedback
        {
            Rating = request.Rating,
            Comment = request.Comment,
            Issues = request.Issues ?? new List<string>()
        };

        var result = await sessionService.AddTurnFeedbackAsync(id, turnId, feedback);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound>> EndSession(
        Guid id,
        ISessionService sessionService)
    {
        var result = await sessionService.EndSessionAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(
        Guid id,
        ISessionService sessionService)
    {
        var result = await sessionService.DeleteAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<SessionStatsResponse>> GetSessionStats(
        [FromQuery] Guid userId,
        ISessionService sessionService)
    {
        var result = await sessionService.GetStatsAsync(userId);
        var stats = result.Value;

        var response = new SessionStatsResponse(
            stats.UserId,
            stats.TotalSessions,
            stats.ActiveSessions,
            stats.TotalTurns,
            stats.TotalQueriesThisWeek,
            stats.TotalQueriesThisMonth,
            stats.AverageSessionDuration,
            stats.AverageTurnsPerSession,
            stats.SessionsByType.ToDictionary(k => k.Key.ToString(), v => v.Value),
            stats.FeedbackByRating.ToDictionary(k => k.Key.ToString(), v => v.Value),
            stats.LastSessionAt,
            stats.TopTags);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<SessionShareResponse>, NotFound>> ShareSession(
        Guid id,
        ShareSessionApiRequest request,
        ISessionService sessionService)
    {
        var shareRequest = new ShareSessionRequest
        {
            ShareWithUserId = request.ShareWithUserId,
            ShareWithTeamId = request.ShareWithTeamId,
            IsPublic = request.IsPublic,
            Permission = request.Permission ?? SessionSharePermission.ReadOnly,
            ExpiresAt = request.ExpiresAt
        };

        var result = await sessionService.ShareAsync(id, shareRequest);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var share = result.Value;
        var response = new SessionShareResponse(
            share.Id,
            share.SessionId,
            share.SharedWithUserId,
            share.SharedWithTeamId,
            share.IsPublic,
            share.Permission.ToString(),
            share.ShareLink,
            share.CreatedAt,
            share.ExpiresAt);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<List<SessionResponse>>> GetSharedSessions(
        [FromQuery] Guid userId,
        ISessionService sessionService)
    {
        var result = await sessionService.GetSharedWithUserAsync(userId);
        var sessions = result.Value.Select(MapToResponse).ToList();
        return TypedResults.Ok(sessions);
    }

    private static async Task<Results<Ok<SessionExportResponse>, NotFound>> ExportSession(
        Guid id,
        [FromQuery] SessionExportFormat format,
        ISessionService sessionService)
    {
        var result = await sessionService.ExportAsync(id, format);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var export = result.Value;
        var response = new SessionExportResponse(
            export.SessionId,
            export.Format.ToString(),
            export.Content,
            export.FileName,
            export.ContentType,
            export.ExportedAt);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<SessionPageResponse>> SearchSessions(
        [FromQuery] string query,
        [FromQuery] Guid? userId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ISessionService sessionService)
    {
        var filter = new SessionFilter
        {
            PageNumber = page ?? 1,
            PageSize = pageSize ?? 20
        };

        var result = await sessionService.SearchAsync(query, userId, filter);

        var response = new SessionPageResponse(
            result.Value.Items.Select(MapToResponse).ToList(),
            result.Value.TotalCount,
            result.Value.PageNumber,
            result.Value.PageSize,
            result.Value.TotalPages,
            result.Value.HasNextPage,
            result.Value.HasPreviousPage);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<SessionPageResponse>> GetRecentSessions(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ISessionService sessionService)
    {
        var filter = new SessionFilter
        {
            PageNumber = page ?? 1,
            PageSize = pageSize ?? 20
        };

        var result = await sessionService.GetRecentAsync(filter);

        var response = new SessionPageResponse(
            result.Value.Items.Select(MapToResponse).ToList(),
            result.Value.TotalCount,
            result.Value.PageNumber,
            result.Value.PageSize,
            result.Value.TotalPages,
            result.Value.HasNextPage,
            result.Value.HasPreviousPage);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ConversationContextResponse>, NotFound>> BuildContext(
        Guid id,
        [FromQuery] int? maxTurns,
        [FromQuery] int? maxTokens,
        IConversationContextService contextService)
    {
        var options = new BuildContextOptions
        {
            MaxTurns = maxTurns ?? 10,
            MaxTokens = maxTokens ?? 4000
        };

        var result = await contextService.BuildContextAsync(id, options);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var context = result.Value;
        var response = new ConversationContextResponse(
            context.SessionId,
            context.Messages.Select(m => new ContextMessageResponse(
                m.Role.ToString(),
                m.Content,
                m.TurnNumber,
                m.Timestamp,
                m.EstimatedTokens)).ToList(),
            context.Entities.Select(e => new TrackedEntityResponse(
                e.Name,
                e.Type.ToString(),
                e.MentionCount,
                e.Salience)).ToList(),
            context.CurrentTopic != null ? new TopicResponse(
                context.CurrentTopic.Name,
                context.CurrentTopic.Keywords,
                context.CurrentTopic.Confidence) : null,
            context.EstimatedTokens);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<RewrittenQueryResponse>, NotFound>> RewriteQuery(
        Guid id,
        RewriteQueryRequest request,
        IConversationContextService contextService)
    {
        var contextResult = await contextService.BuildContextAsync(id);
        if (contextResult.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var result = await contextService.RewriteQueryAsync(request.Query, contextResult.Value);

        var rewritten = result.Value;
        var response = new RewrittenQueryResponse(
            rewritten.OriginalQuery,
            rewritten.RewrittenText,
            rewritten.ResolvedReferences,
            rewritten.ExpandedTerms,
            rewritten.ConfidenceScore,
            rewritten.Explanation);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<TopicResponse>, NotFound>> GetCurrentTopic(
        Guid id,
        IConversationContextService contextService)
    {
        var result = await contextService.GetCurrentTopicAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var topic = result.Value;
        var response = new TopicResponse(topic.Name, topic.Keywords, topic.Confidence);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<TopicShiftResponse>, NotFound>> DetectTopicShift(
        Guid id,
        TopicShiftRequest request,
        IConversationContextService contextService)
    {
        var result = await contextService.DetectTopicShiftAsync(id, request.Query);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var shift = result.Value;
        var response = new TopicShiftResponse(
            shift.PreviousTopic?.Name,
            shift.NewTopic?.Name,
            shift.IsSignificantShift,
            shift.ShiftScore,
            shift.Reason);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ConversationSummaryResponse>, NotFound>> GetConversationSummary(
        Guid id,
        [FromQuery] int? maxLength,
        IConversationContextService contextService)
    {
        var options = new SummarizeOptions
        {
            MaxLength = maxLength ?? 500
        };

        var result = await contextService.SummarizeConversationAsync(id, options);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var summary = result.Value;
        var response = new ConversationSummaryResponse(
            summary.SessionId,
            summary.Summary,
            summary.KeyPoints,
            summary.QuestionsAsked,
            summary.TopicsDiscussed,
            summary.TurnsCovered);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<List<string>>, NotFound>> GenerateFollowUps(
        Guid id,
        GenerateFollowUpsRequest request,
        IConversationContextService contextService)
    {
        var result = await contextService.GenerateFollowUpsAsync(
            id, request.LastResponse, request.Count ?? 3);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Value.ToList());
    }

    private static Ok<List<SessionTemplateInfo>> GetSessionTemplates()
    {
        var templates = new List<SessionTemplateInfo>
        {
            new("quick-query", "Quick Query", "Fast Q&A session with minimal context"),
            new("research", "Research Session", "In-depth research with full context tracking"),
            new("analysis", "Analysis Session", "Document analysis with citation support"),
            new("comparison", "Comparison Session", "Compare and contrast topics or documents"),
            new("summary", "Summary Session", "Summarize documents or topics")
        };

        return TypedResults.Ok(templates);
    }

    #region Mapping Helpers

    private static SessionResponse MapToResponse(Session session) => new(
        session.Id,
        session.UserId,
        session.TeamId,
        session.WorkspaceId,
        session.Title,
        session.Description,
        session.Status.ToString(),
        session.Type.ToString(),
        session.TurnCount,
        session.Tags,
        session.CreatedAt,
        session.LastActivityAt,
        session.EndedAt,
        session.Duration?.TotalMinutes);

    private static SessionTurnResponse MapTurnToResponse(SessionTurn turn) => new(
        turn.Id,
        turn.SessionId,
        turn.TurnNumber,
        turn.UserQuery,
        turn.SystemResponse,
        turn.Status.ToString(),
        turn.Sources.Select(s => new SourceReferenceResponse(
            s.DocumentId,
            s.DocumentName,
            s.Excerpt,
            s.RelevanceScore,
            s.PageNumber)).ToList(),
        turn.FollowUpQuestions,
        turn.Metrics != null ? new TurnMetricsResponse(
            turn.Metrics.RetrievalTime.TotalMilliseconds,
            turn.Metrics.GenerationTime.TotalMilliseconds,
            turn.Metrics.TotalTime.TotalMilliseconds,
            turn.Metrics.SourcesRetrieved,
            turn.Metrics.SourcesUsed,
            turn.Metrics.TokensUsed,
            turn.Metrics.CacheHit) : null,
        turn.Feedback != null ? new TurnFeedbackResponse(
            turn.Feedback.Rating.ToString(),
            turn.Feedback.Comment,
            turn.Feedback.ProvidedAt) : null,
        turn.CreatedAt,
        turn.CompletedAt,
        turn.ProcessingTime?.TotalMilliseconds);

    #endregion
}

#region Request/Response DTOs

public record CreateSessionApiRequest(
    Guid UserId,
    Guid? TeamId = null,
    Guid? WorkspaceId = null,
    string? Title = null,
    string? Description = null,
    SessionType? Type = null,
    List<string>? Tags = null,
    SessionSettings? Settings = null,
    Dictionary<string, object>? Metadata = null);

public record UpdateSessionApiRequest(
    string? Title = null,
    string? Description = null,
    List<string>? Tags = null,
    SessionSettings? Settings = null,
    Dictionary<string, object>? Metadata = null);

public record UpdateTitleRequest(string Title);

public record AddTurnApiRequest(
    string Query,
    Dictionary<string, object>? Context = null);

public record CompleteTurnRequest(
    string Response,
    List<SessionSourceReference>? Sources = null,
    List<string>? FollowUps = null,
    TurnMetrics? Metrics = null);

public record TurnFeedbackRequest(
    FeedbackRating Rating,
    string? Comment = null,
    List<string>? Issues = null);

public record ShareSessionApiRequest(
    Guid? ShareWithUserId = null,
    Guid? ShareWithTeamId = null,
    bool IsPublic = false,
    SessionSharePermission? Permission = null,
    DateTime? ExpiresAt = null);

public record RewriteQueryRequest(string Query);

public record TopicShiftRequest(string Query);

public record GenerateFollowUpsRequest(string LastResponse, int? Count = null);

public record SessionResponse(
    Guid Id,
    Guid UserId,
    Guid? TeamId,
    Guid? WorkspaceId,
    string Title,
    string? Description,
    string Status,
    string Type,
    int TurnCount,
    List<string> Tags,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    DateTime? EndedAt,
    double? DurationMinutes);

public record SessionPageResponse(
    List<SessionResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public record SessionTurnResponse(
    Guid Id,
    Guid SessionId,
    int TurnNumber,
    string UserQuery,
    string? SystemResponse,
    string Status,
    List<SourceReferenceResponse> Sources,
    List<string> FollowUpQuestions,
    TurnMetricsResponse? Metrics,
    TurnFeedbackResponse? Feedback,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    double? ProcessingTimeMs);

public record SourceReferenceResponse(
    Guid DocumentId,
    string DocumentName,
    string? Excerpt,
    double RelevanceScore,
    int? PageNumber);

public record TurnMetricsResponse(
    double RetrievalTimeMs,
    double GenerationTimeMs,
    double TotalTimeMs,
    int SourcesRetrieved,
    int SourcesUsed,
    int TokensUsed,
    bool? CacheHit);

public record TurnFeedbackResponse(
    string Rating,
    string? Comment,
    DateTime ProvidedAt);

public record SessionStatsResponse(
    Guid UserId,
    int TotalSessions,
    int ActiveSessions,
    int TotalTurns,
    int TotalQueriesThisWeek,
    int TotalQueriesThisMonth,
    double AverageSessionDuration,
    double AverageTurnsPerSession,
    Dictionary<string, int> SessionsByType,
    Dictionary<string, int> FeedbackByRating,
    DateTime? LastSessionAt,
    List<string> TopTags);

public record SessionShareResponse(
    Guid Id,
    Guid SessionId,
    Guid? SharedWithUserId,
    Guid? SharedWithTeamId,
    bool IsPublic,
    string Permission,
    string? ShareLink,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public record SessionExportResponse(
    Guid SessionId,
    string Format,
    string Content,
    string? FileName,
    string? ContentType,
    DateTime ExportedAt);

public record ConversationContextResponse(
    Guid SessionId,
    List<ContextMessageResponse> Messages,
    List<TrackedEntityResponse> Entities,
    TopicResponse? CurrentTopic,
    int EstimatedTokens);

public record ContextMessageResponse(
    string Role,
    string Content,
    int TurnNumber,
    DateTime Timestamp,
    int EstimatedTokens);

public record TrackedEntityResponse(
    string Name,
    string Type,
    int MentionCount,
    double Salience);

public record TopicResponse(
    string Name,
    List<string> Keywords,
    double Confidence);

public record TopicShiftResponse(
    string? PreviousTopic,
    string? NewTopic,
    bool IsSignificantShift,
    double ShiftScore,
    string? Reason);

public record RewrittenQueryResponse(
    string OriginalQuery,
    string RewrittenText,
    List<string> ResolvedReferences,
    List<string> ExpandedTerms,
    double ConfidenceScore,
    string? Explanation);

public record ConversationSummaryResponse(
    Guid SessionId,
    string Summary,
    List<string> KeyPoints,
    List<string> QuestionsAsked,
    List<string> TopicsDiscussed,
    int TurnsCovered);

public record SessionTemplateInfo(string Key, string Name, string Description);

#endregion
