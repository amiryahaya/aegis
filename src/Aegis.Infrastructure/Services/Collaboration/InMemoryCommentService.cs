using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Collaboration;

/// <summary>
/// In-memory implementation of comment service
/// </summary>
public partial class InMemoryCommentService : ICommentService
{
    private readonly ConcurrentDictionary<Guid, Comment> _comments = new();
    private readonly ConcurrentDictionary<Guid, List<CommentReaction>> _reactions = new();
    private readonly ConcurrentDictionary<(Guid CommentId, Guid UserId), bool> _mentionReadStatus = new();

    public Task<Result<Comment>> CreateCommentAsync(
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Parse mentions from content
        var mentions = ParseMentions(request.Content, request.MentionedUserIds);

        // Determine thread root
        Guid? threadRootId = null;
        if (request.ParentCommentId.HasValue)
        {
            if (_comments.TryGetValue(request.ParentCommentId.Value, out var parent))
            {
                threadRootId = parent.ThreadRootId ?? parent.Id;
            }
            else
            {
                return Task.FromResult(Result<Comment>.Failure(
                    Error.NotFound("Comment.ParentNotFound", "Parent comment not found")));
            }
        }

        var comment = new Comment
        {
            Id = UuidGenerator.NewId(),
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            AuthorId = request.AuthorId,
            AuthorName = request.AuthorName,
            AuthorAvatarUrl = request.AuthorAvatarUrl,
            Content = request.Content,
            HtmlContent = ConvertToHtml(request.Content),
            ParentCommentId = request.ParentCommentId,
            ThreadRootId = threadRootId,
            Anchor = request.Anchor,
            Mentions = mentions,
            Attachments = request.Attachments ?? new List<CommentAttachment>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = CommentStatus.Active
        };

        _comments[comment.Id] = comment;

        // Initialize reactions list
        _reactions[comment.Id] = new List<CommentReaction>();

        // Update parent reply count if this is a reply
        if (request.ParentCommentId.HasValue && _comments.TryGetValue(request.ParentCommentId.Value, out var parentComment))
        {
            _comments[request.ParentCommentId.Value] = parentComment with
            {
                ReplyCount = parentComment.ReplyCount + 1
            };
        }

        return Task.FromResult(Result<Comment>.Success(comment));
    }

    public Task<Result<Comment>> UpdateCommentAsync(
        Guid commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        if (comment.AuthorId != request.UpdatedBy)
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.Validation("Comment.NotAuthor", "Only the author can edit this comment")));
        }

        var mentions = ParseMentions(request.Content, request.MentionedUserIds);

        var updatedComment = comment with
        {
            Content = request.Content,
            HtmlContent = ConvertToHtml(request.Content),
            Mentions = mentions,
            UpdatedAt = DateTime.UtcNow,
            EditedAt = DateTime.UtcNow,
            IsEdited = true
        };

        _comments[commentId] = updatedComment;

        return Task.FromResult(Result<Comment>.Success(updatedComment));
    }

    public Task<Result> DeleteCommentAsync(
        Guid commentId,
        Guid deletedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        // Soft delete
        var deletedComment = comment with
        {
            Status = CommentStatus.Deleted,
            Content = "[deleted]",
            HtmlContent = "<p>[deleted]</p>",
            UpdatedAt = DateTime.UtcNow
        };

        _comments[commentId] = deletedComment;

        return Task.FromResult(Result.Success());
    }

    public Task<Result<Comment>> GetCommentByIdAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        // Add reaction counts
        var reactionCounts = GetReactionCounts(commentId);
        var commentWithReactions = comment with { ReactionCounts = reactionCounts };

        return Task.FromResult(Result<Comment>.Success(commentWithReactions));
    }

    public Task<Result<CommentPage>> GetCommentsForResourceAsync(
        Guid resourceId,
        CommentableResourceType resourceType,
        CommentFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new CommentFilter();

        var query = _comments.Values.Where(c =>
            c.ResourceId == resourceId &&
            c.ResourceType == resourceType &&
            c.Status == CommentStatus.Active);

        // Only include top-level comments by default
        if (filter.IncludeReplies != true)
        {
            query = query.Where(c => c.ParentCommentId == null);
        }

        if (filter.OnlyResolved == true)
            query = query.Where(c => c.IsResolved);

        if (filter.OnlyUnresolved == true)
            query = query.Where(c => !c.IsResolved);

        if (filter.OnlyPinned == true)
            query = query.Where(c => c.IsPinned);

        if (filter.AuthorId.HasValue)
            query = query.Where(c => c.AuthorId == filter.AuthorId.Value);

        if (filter.Since.HasValue)
            query = query.Where(c => c.CreatedAt >= filter.Since.Value);

        var totalCount = query.Count();

        // Apply sorting
        query = filter.SortOrder switch
        {
            CommentSortOrder.Oldest => query.OrderBy(c => c.CreatedAt),
            CommentSortOrder.MostReplies => query.OrderByDescending(c => c.ReplyCount),
            CommentSortOrder.MostReactions => query.OrderByDescending(c => GetTotalReactions(c.Id)),
            _ => query.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.CreatedAt)
        };

        var comments = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => c with { ReactionCounts = GetReactionCounts(c.Id) })
            .ToList();

        var page = new CommentPage
        {
            Comments = comments,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return Task.FromResult(Result<CommentPage>.Success(page));
    }

    public Task<Result<IReadOnlyList<Comment>>> GetRepliesAsync(
        Guid parentCommentId,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.ContainsKey(parentCommentId))
        {
            return Task.FromResult(Result<IReadOnlyList<Comment>>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {parentCommentId} not found")));
        }

        var replies = _comments.Values
            .Where(c => c.ParentCommentId == parentCommentId && c.Status == CommentStatus.Active)
            .OrderBy(c => c.CreatedAt)
            .Select(c => c with { ReactionCounts = GetReactionCounts(c.Id) })
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<Comment>>.Success(replies));
    }

    public Task<Result<CommentReaction>> AddReactionAsync(
        Guid commentId,
        AddReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.ContainsKey(commentId))
        {
            return Task.FromResult(Result<CommentReaction>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var reactions = _reactions.GetOrAdd(commentId, _ => new List<CommentReaction>());

        // Check if user already reacted with this type
        var existingReaction = reactions.FirstOrDefault(r =>
            r.UserId == request.UserId && r.Type == request.Type);

        if (existingReaction != null)
        {
            return Task.FromResult(Result<CommentReaction>.Failure(
                Error.Conflict("Reaction.AlreadyExists", "You have already added this reaction")));
        }

        var reaction = new CommentReaction
        {
            CommentId = commentId,
            UserId = request.UserId,
            UserName = request.UserName,
            Type = request.Type,
            CreatedAt = DateTime.UtcNow
        };

        reactions.Add(reaction);

        return Task.FromResult(Result<CommentReaction>.Success(reaction));
    }

    public Task<Result> RemoveReactionAsync(
        Guid commentId,
        Guid userId,
        ReactionType reactionType,
        CancellationToken cancellationToken = default)
    {
        if (!_reactions.TryGetValue(commentId, out var reactions))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var reaction = reactions.FirstOrDefault(r => r.UserId == userId && r.Type == reactionType);
        if (reaction == null)
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Reaction.NotFound", "Reaction not found")));
        }

        reactions.Remove(reaction);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<IReadOnlyList<CommentReaction>>> GetReactionsAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.ContainsKey(commentId))
        {
            return Task.FromResult(Result<IReadOnlyList<CommentReaction>>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var reactions = _reactions.GetOrAdd(commentId, _ => new List<CommentReaction>());

        return Task.FromResult(Result<IReadOnlyList<CommentReaction>>.Success(reactions));
    }

    public Task<Result<Comment>> ResolveCommentAsync(
        Guid commentId,
        Guid resolvedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var resolvedComment = comment with
        {
            IsResolved = true,
            ResolvedBy = resolvedBy,
            ResolvedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _comments[commentId] = resolvedComment;

        return Task.FromResult(Result<Comment>.Success(resolvedComment));
    }

    public Task<Result<Comment>> ReopenCommentAsync(
        Guid commentId,
        Guid reopenedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var reopenedComment = comment with
        {
            IsResolved = false,
            ResolvedBy = null,
            ResolvedAt = null,
            UpdatedAt = DateTime.UtcNow
        };

        _comments[commentId] = reopenedComment;

        return Task.FromResult(Result<Comment>.Success(reopenedComment));
    }

    public Task<Result<Comment>> PinCommentAsync(
        Guid commentId,
        Guid pinnedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var pinnedComment = comment with
        {
            IsPinned = true,
            PinnedBy = pinnedBy,
            PinnedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _comments[commentId] = pinnedComment;

        return Task.FromResult(Result<Comment>.Success(pinnedComment));
    }

    public Task<Result<Comment>> UnpinCommentAsync(
        Guid commentId,
        Guid unpinnedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_comments.TryGetValue(commentId, out var comment))
        {
            return Task.FromResult(Result<Comment>.Failure(
                Error.NotFound("Comment.NotFound", $"Comment with ID {commentId} not found")));
        }

        var unpinnedComment = comment with
        {
            IsPinned = false,
            PinnedBy = null,
            PinnedAt = null,
            UpdatedAt = DateTime.UtcNow
        };

        _comments[commentId] = unpinnedComment;

        return Task.FromResult(Result<Comment>.Success(unpinnedComment));
    }

    public Task<Result<MentionPage>> GetMentionsForUserAsync(
        Guid userId,
        MentionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new MentionFilter();

        var query = _comments.Values
            .Where(c => c.Status == CommentStatus.Active &&
                        c.Mentions.Any(m => m.UserId == userId));

        if (filter.ResourceType.HasValue)
            query = query.Where(c => c.ResourceType == filter.ResourceType.Value);

        if (filter.Since.HasValue)
            query = query.Where(c => c.CreatedAt >= filter.Since.Value);

        var mentions = query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new MentionInfo
            {
                CommentId = c.Id,
                ResourceId = c.ResourceId,
                ResourceType = c.ResourceType,
                MentionedBy = c.AuthorId,
                MentionedByName = c.AuthorName,
                CommentContent = c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content,
                MentionedAt = c.CreatedAt,
                IsRead = _mentionReadStatus.ContainsKey((c.Id, userId))
            })
            .ToList();

        var totalCount = mentions.Count;
        var unreadCount = mentions.Count(m => !m.IsRead);

        if (filter.OnlyUnread == true)
            mentions = mentions.Where(m => !m.IsRead).ToList();

        var pagedMentions = mentions
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var page = new MentionPage
        {
            Mentions = pagedMentions,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return Task.FromResult(Result<MentionPage>.Success(page));
    }

    public Task<Result> MarkMentionsAsReadAsync(
        Guid userId,
        IEnumerable<Guid>? commentIds = null,
        CancellationToken cancellationToken = default)
    {
        if (commentIds != null)
        {
            foreach (var commentId in commentIds)
            {
                _mentionReadStatus[(commentId, userId)] = true;
            }
        }
        else
        {
            // Mark all mentions as read
            var userMentions = _comments.Values
                .Where(c => c.Mentions.Any(m => m.UserId == userId))
                .Select(c => c.Id);

            foreach (var commentId in userMentions)
            {
                _mentionReadStatus[(commentId, userId)] = true;
            }
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<CommentPage>> SearchCommentsAsync(
        CommentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _comments.Values.Where(c =>
            c.Status == CommentStatus.Active &&
            c.Content.Contains(request.Query, StringComparison.OrdinalIgnoreCase));

        if (request.ResourceType.HasValue)
            query = query.Where(c => c.ResourceType == request.ResourceType.Value);

        if (request.AuthorId.HasValue)
            query = query.Where(c => c.AuthorId == request.AuthorId.Value);

        if (request.StartDate.HasValue)
            query = query.Where(c => c.CreatedAt >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(c => c.CreatedAt <= request.EndDate.Value);

        var totalCount = query.Count();

        var comments = query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => c with { ReactionCounts = GetReactionCounts(c.Id) })
            .ToList();

        var page = new CommentPage
        {
            Comments = comments,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Task.FromResult(Result<CommentPage>.Success(page));
    }

    public Task<Result<CommentStats>> GetStatsAsync(
        Guid? resourceId = null,
        CommentableResourceType? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _comments.Values.Where(c => c.Status == CommentStatus.Active);

        if (resourceId.HasValue)
            query = query.Where(c => c.ResourceId == resourceId.Value);

        if (resourceType.HasValue)
            query = query.Where(c => c.ResourceType == resourceType.Value);

        var comments = query.ToList();
        var threads = comments.Where(c => c.ParentCommentId == null).ToList();
        var replies = comments.Where(c => c.ParentCommentId != null).ToList();

        var allReactions = _reactions.Values.SelectMany(r => r).ToList();
        var allMentions = comments.SelectMany(c => c.Mentions).ToList();

        var stats = new CommentStats
        {
            TotalComments = comments.Count,
            TotalThreads = threads.Count,
            TotalReplies = replies.Count,
            ResolvedThreads = threads.Count(t => t.IsResolved),
            UnresolvedThreads = threads.Count(t => !t.IsResolved),
            TotalReactions = allReactions.Count,
            TotalMentions = allMentions.Count,
            UnreadMentions = allMentions.Count(m => !_mentionReadStatus.ContainsKey((
                comments.First(c => c.Mentions.Contains(m)).Id,
                m.UserId))),
            CommentsByResourceType = comments
                .GroupBy(c => c.ResourceType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ReactionsByType = allReactions
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopCommenters = comments
                .GroupBy(c => c.AuthorId)
                .Select(g => new TopCommenter
                {
                    UserId = g.Key,
                    DisplayName = g.First().AuthorName,
                    CommentCount = g.Count(),
                    ReactionCount = allReactions.Count(r => g.Select(c => c.Id).Contains(r.CommentId))
                })
                .OrderByDescending(t => t.CommentCount)
                .Take(10)
                .ToList(),
            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(Result<CommentStats>.Success(stats));
    }

    private Dictionary<ReactionType, int> GetReactionCounts(Guid commentId)
    {
        if (!_reactions.TryGetValue(commentId, out var reactions))
            return new Dictionary<ReactionType, int>();

        return reactions
            .GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private int GetTotalReactions(Guid commentId)
    {
        if (!_reactions.TryGetValue(commentId, out var reactions))
            return 0;
        return reactions.Count;
    }

    private static List<CommentMention> ParseMentions(string content, List<Guid>? mentionedUserIds)
    {
        var mentions = new List<CommentMention>();

        // Simple @username pattern
        var regex = MentionRegex();
        var matches = regex.Matches(content);

        var index = 0;
        foreach (Match match in matches)
        {
            var userId = mentionedUserIds != null && index < mentionedUserIds.Count
                ? mentionedUserIds[index]
                : Guid.Empty;

            mentions.Add(new CommentMention
            {
                UserId = userId,
                DisplayName = match.Groups[1].Value,
                StartPosition = match.Index,
                EndPosition = match.Index + match.Length
            });
            index++;
        }

        return mentions;
    }

    private static string ConvertToHtml(string content)
    {
        // Simple conversion - escape HTML and convert mentions to links
        var escaped = System.Web.HttpUtility.HtmlEncode(content);
        var withMentions = MentionRegex().Replace(escaped, "<span class=\"mention\">@$1</span>");
        return $"<p>{withMentions}</p>";
    }

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex MentionRegex();
}
