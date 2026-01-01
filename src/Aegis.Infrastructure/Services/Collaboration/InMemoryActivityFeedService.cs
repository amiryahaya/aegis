using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Collaboration;

/// <summary>
/// In-memory implementation of activity feed service
/// </summary>
public class InMemoryActivityFeedService : IActivityFeedService
{
    private readonly ConcurrentDictionary<Guid, Activity> _activities = new();
    private readonly ConcurrentDictionary<Guid, ActivitySubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<(Guid UserId, Guid ActivityId), bool> _seenActivities = new();

    public Task<Result<Activity>> RecordActivityAsync(
        RecordActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var description = GenerateDescription(request);

        var activity = new Activity
        {
            Id = UuidGenerator.NewId(),
            ActorId = request.ActorId,
            ActorName = request.ActorName,
            ActorAvatarUrl = request.ActorAvatarUrl,
            Type = request.Type,
            Verb = request.Verb,
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            ResourceName = request.ResourceName,
            TargetId = request.TargetId,
            TargetType = request.TargetType,
            TargetName = request.TargetName,
            WorkspaceId = request.WorkspaceId,
            WorkspaceName = request.WorkspaceName,
            Description = description,
            HtmlDescription = $"<p>{System.Web.HttpUtility.HtmlEncode(description)}</p>",
            Visibility = request.Visibility,
            Importance = request.Importance,
            Details = request.Details ?? new Dictionary<string, object>(),
            RelatedUserIds = request.RelatedUserIds ?? new List<Guid>(),
            OccurredAt = DateTime.UtcNow
        };

        _activities[activity.Id] = activity;

        return Task.FromResult(Result<Activity>.Success(activity));
    }

    public Task<Result<ActivityPage>> GetWorkspaceActivitiesAsync(
        Guid workspaceId,
        ActivityFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ActivityFilter();

        var query = _activities.Values.Where(a => a.WorkspaceId == workspaceId);
        query = ApplyFilter(query, filter);

        return Task.FromResult(CreatePage(query, filter));
    }

    public Task<Result<ActivityPage>> GetResourceActivitiesAsync(
        Guid resourceId,
        ActivityResourceType resourceType,
        ActivityFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ActivityFilter();

        var query = _activities.Values.Where(a =>
            a.ResourceId == resourceId && a.ResourceType == resourceType);
        query = ApplyFilter(query, filter);

        return Task.FromResult(CreatePage(query, filter));
    }

    public Task<Result<ActivityPage>> GetUserActivitiesAsync(
        Guid userId,
        ActivityFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ActivityFilter();

        var query = _activities.Values.Where(a => a.ActorId == userId);
        query = ApplyFilter(query, filter);

        return Task.FromResult(CreatePage(query, filter));
    }

    public Task<Result<ActivityPage>> GetPersonalizedFeedAsync(
        Guid userId,
        PersonalizedFeedOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PersonalizedFeedOptions();

        // Get user's subscriptions
        var subscriptions = _subscriptions.Values
            .Where(s => s.UserId == userId)
            .ToList();

        var query = _activities.Values.AsEnumerable();

        // Filter by workspaces if specified
        if (options.WorkspaceIds?.Count > 0)
        {
            query = query.Where(a => a.WorkspaceId.HasValue && options.WorkspaceIds.Contains(a.WorkspaceId.Value));
        }

        // Exclude own activities if specified
        if (!options.IncludeOwnActivities)
        {
            query = query.Where(a => a.ActorId != userId);
        }

        // Filter by subscribed resources if specified
        if (options.OnlySubscribed && subscriptions.Count > 0)
        {
            query = query.Where(a =>
                subscriptions.Any(s =>
                    (s.Scope == SubscriptionScope.Workspace && s.WorkspaceId == a.WorkspaceId) ||
                    (s.Scope == SubscriptionScope.Resource && s.ResourceId == a.ResourceId) ||
                    s.Scope == SubscriptionScope.All));
        }

        // Filter unseen if specified
        if (options.OnlyUnseen)
        {
            query = query.Where(a => !_seenActivities.ContainsKey((userId, a.Id)));
        }

        // Include activities where user is related
        query = query.Where(a =>
            a.RelatedUserIds.Contains(userId) ||
            a.Visibility >= ActivityVisibility.Team);

        var totalCount = query.Count();

        var activities = query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToList();

        var page = new ActivityPage
        {
            Activities = activities,
            TotalCount = totalCount,
            Page = options.Page,
            PageSize = options.PageSize
        };

        return Task.FromResult(Result<ActivityPage>.Success(page));
    }

    public Task<Result<Activity>> GetActivityByIdAsync(
        Guid activityId,
        CancellationToken cancellationToken = default)
    {
        if (!_activities.TryGetValue(activityId, out var activity))
        {
            return Task.FromResult(Result<Activity>.Failure(
                Error.NotFound("Activity.NotFound", $"Activity with ID {activityId} not found")));
        }

        return Task.FromResult(Result<Activity>.Success(activity));
    }

    public Task<Result> MarkAsSeenAsync(
        Guid userId,
        IEnumerable<Guid>? activityIds = null,
        CancellationToken cancellationToken = default)
    {
        if (activityIds != null)
        {
            foreach (var activityId in activityIds)
            {
                _seenActivities[(userId, activityId)] = true;
            }
        }
        else
        {
            // Mark all as seen
            foreach (var activity in _activities.Values)
            {
                _seenActivities[(userId, activity.Id)] = true;
            }
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<int>> GetUnseenCountAsync(
        Guid userId,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _activities.Values.AsEnumerable();

        if (workspaceId.HasValue)
        {
            query = query.Where(a => a.WorkspaceId == workspaceId);
        }

        // Exclude own activities
        query = query.Where(a => a.ActorId != userId);

        // Filter to activities user can see
        query = query.Where(a =>
            a.RelatedUserIds.Contains(userId) ||
            a.Visibility >= ActivityVisibility.Team);

        var unseenCount = query.Count(a => !_seenActivities.ContainsKey((userId, a.Id)));

        return Task.FromResult(Result<int>.Success(unseenCount));
    }

    public Task<Result<ActivitySubscription>> SubscribeAsync(
        SubscribeToActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check for existing subscription
        var existing = _subscriptions.Values.FirstOrDefault(s =>
            s.UserId == request.UserId &&
            s.ResourceId == request.ResourceId &&
            s.ResourceType == request.ResourceType &&
            s.WorkspaceId == request.WorkspaceId);

        if (existing != null)
        {
            return Task.FromResult(Result<ActivitySubscription>.Failure(
                Error.Conflict("Subscription.AlreadyExists", "Subscription already exists")));
        }

        var subscription = new ActivitySubscription
        {
            Id = UuidGenerator.NewId(),
            UserId = request.UserId,
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            WorkspaceId = request.WorkspaceId,
            Scope = request.Scope,
            IncludedVerbs = request.IncludedVerbs ?? new List<ActivityVerb>(),
            ExcludedVerbs = request.ExcludedVerbs ?? new List<ActivityVerb>(),
            NotifyEmail = request.NotifyEmail,
            NotifyInApp = request.NotifyInApp,
            NotifyRealTime = request.NotifyRealTime,
            CreatedAt = DateTime.UtcNow
        };

        _subscriptions[subscription.Id] = subscription;

        return Task.FromResult(Result<ActivitySubscription>.Success(subscription));
    }

    public Task<Result> UnsubscribeAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out _))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Subscription.NotFound", $"Subscription with ID {subscriptionId} not found")));
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<IReadOnlyList<ActivitySubscription>>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = _subscriptions.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ActivitySubscription>>.Success(subscriptions));
    }

    public Task<Result<IReadOnlyList<ActivityGroup>>> GetAggregatedActivitiesAsync(
        Guid workspaceId,
        AggregationOptions options,
        CancellationToken cancellationToken = default)
    {
        var query = _activities.Values.Where(a => a.WorkspaceId == workspaceId);

        if (options.Since.HasValue)
        {
            query = query.Where(a => a.OccurredAt >= options.Since.Value);
        }

        var activities = query.OrderByDescending(a => a.OccurredAt).ToList();

        var groups = new List<ActivityGroup>();
        var grouped = new Dictionary<string, List<Activity>>();

        foreach (var activity in activities)
        {
            var key = GenerateGroupKey(activity, options);
            if (!grouped.ContainsKey(key))
            {
                grouped[key] = new List<Activity>();
            }
            grouped[key].Add(activity);
        }

        foreach (var (key, groupActivities) in grouped.Take(options.MaxGroups))
        {
            var first = groupActivities.First();
            var group = new ActivityGroup
            {
                GroupKey = key,
                ActorId = options.GroupByActor ? first.ActorId : null,
                ActorName = options.GroupByActor ? first.ActorName : null,
                ResourceId = options.GroupByResource ? first.ResourceId : null,
                ResourceType = options.GroupByResource ? first.ResourceType : null,
                ResourceName = options.GroupByResource ? first.ResourceName : null,
                Verb = options.GroupByVerb ? first.Verb : null,
                Count = groupActivities.Count,
                FirstOccurrence = groupActivities.Min(a => a.OccurredAt),
                LastOccurrence = groupActivities.Max(a => a.OccurredAt),
                Summary = GenerateGroupSummary(groupActivities, options),
                SampleActivities = groupActivities.Take(3).ToList()
            };

            groups.Add(group);
        }

        return Task.FromResult(Result<IReadOnlyList<ActivityGroup>>.Success(groups));
    }

    public Task<Result<int>> PurgeOldActivitiesAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - retention;
        var oldActivities = _activities.Values
            .Where(a => a.OccurredAt < cutoff)
            .Select(a => a.Id)
            .ToList();

        var purgedCount = 0;
        foreach (var activityId in oldActivities)
        {
            if (_activities.TryRemove(activityId, out _))
            {
                purgedCount++;
            }
        }

        // Clean up seen status for purged activities
        var seenToRemove = _seenActivities.Keys
            .Where(k => oldActivities.Contains(k.ActivityId))
            .ToList();
        foreach (var key in seenToRemove)
        {
            _seenActivities.TryRemove(key, out _);
        }

        return Task.FromResult(Result<int>.Success(purgedCount));
    }

    public Task<Result<ActivityStats>> GetStatsAsync(
        Guid? workspaceId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _activities.Values.AsEnumerable();

        if (workspaceId.HasValue)
        {
            query = query.Where(a => a.WorkspaceId == workspaceId);
        }

        if (since.HasValue)
        {
            query = query.Where(a => a.OccurredAt >= since.Value);
        }

        var activities = query.ToList();
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var stats = new ActivityStats
        {
            TotalActivities = activities.Count,
            ActivitiesToday = activities.Count(a => a.OccurredAt >= today),
            ActivitiesThisWeek = activities.Count(a => a.OccurredAt >= weekAgo),
            ActiveUsers = activities.Select(a => a.ActorId).Distinct().Count(),
            ByType = activities
                .GroupBy(a => a.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByVerb = activities
                .GroupBy(a => a.Verb)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByResourceType = activities
                .Where(a => a.ResourceType.HasValue)
                .GroupBy(a => a.ResourceType!.Value)
                .ToDictionary(g => g.Key, g => g.Count()),
            Trends = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date = today.AddDays(-i);
                    return new ActivityTrend
                    {
                        Date = date,
                        Count = activities.Count(a => a.OccurredAt.Date == date)
                    };
                })
                .Reverse()
                .ToList(),
            TopUsers = activities
                .GroupBy(a => a.ActorId)
                .Select(g => new TopActivityUser
                {
                    UserId = g.Key,
                    DisplayName = g.First().ActorName,
                    ActivityCount = g.Count()
                })
                .OrderByDescending(u => u.ActivityCount)
                .Take(10)
                .ToList(),
            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(Result<ActivityStats>.Success(stats));
    }

    private static IEnumerable<Activity> ApplyFilter(IEnumerable<Activity> query, ActivityFilter filter)
    {
        if (filter.Type.HasValue)
            query = query.Where(a => a.Type == filter.Type.Value);

        if (filter.Verb.HasValue)
            query = query.Where(a => a.Verb == filter.Verb.Value);

        if (filter.ResourceType.HasValue)
            query = query.Where(a => a.ResourceType == filter.ResourceType.Value);

        if (filter.ActorId.HasValue)
            query = query.Where(a => a.ActorId == filter.ActorId.Value);

        if (filter.MinImportance.HasValue)
            query = query.Where(a => a.Importance >= filter.MinImportance.Value);

        if (filter.Since.HasValue)
            query = query.Where(a => a.OccurredAt >= filter.Since.Value);

        if (filter.Until.HasValue)
            query = query.Where(a => a.OccurredAt <= filter.Until.Value);

        return query;
    }

    private static Result<ActivityPage> CreatePage(IEnumerable<Activity> query, ActivityFilter filter)
    {
        var totalCount = query.Count();

        var activities = query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var page = new ActivityPage
        {
            Activities = activities,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return Result<ActivityPage>.Success(page);
    }

    private static string GenerateDescription(RecordActivityRequest request)
    {
        var actor = request.ActorName;
        var verb = request.Verb.ToString().ToLower();
        var resource = request.ResourceName ?? request.ResourceType?.ToString().ToLower() ?? "resource";
        var target = request.TargetName;

        return request.Verb switch
        {
            ActivityVerb.Created => $"{actor} created {resource}",
            ActivityVerb.Updated => $"{actor} updated {resource}",
            ActivityVerb.Deleted => $"{actor} deleted {resource}",
            ActivityVerb.Shared => target != null
                ? $"{actor} shared {resource} with {target}"
                : $"{actor} shared {resource}",
            ActivityVerb.Commented => $"{actor} commented on {resource}",
            ActivityVerb.Replied => $"{actor} replied to a comment on {resource}",
            ActivityVerb.Mentioned => $"{actor} mentioned you in {resource}",
            ActivityVerb.Joined => $"{actor} joined {resource}",
            ActivityVerb.Left => $"{actor} left {resource}",
            ActivityVerb.Invited => $"{actor} invited {target} to {resource}",
            ActivityVerb.Removed => $"{actor} removed {target} from {resource}",
            ActivityVerb.Queried => $"{actor} queried {resource}",
            ActivityVerb.Viewed => $"{actor} viewed {resource}",
            ActivityVerb.Downloaded => $"{actor} downloaded {resource}",
            ActivityVerb.Exported => $"{actor} exported {resource}",
            ActivityVerb.Uploaded => $"{actor} uploaded {resource}",
            ActivityVerb.Resolved => $"{actor} resolved {resource}",
            ActivityVerb.Reopened => $"{actor} reopened {resource}",
            ActivityVerb.Pinned => $"{actor} pinned {resource}",
            ActivityVerb.Unpinned => $"{actor} unpinned {resource}",
            ActivityVerb.Archived => $"{actor} archived {resource}",
            ActivityVerb.Restored => $"{actor} restored {resource}",
            ActivityVerb.Transferred => target != null
                ? $"{actor} transferred {resource} to {target}"
                : $"{actor} transferred {resource}",
            _ => $"{actor} {verb} {resource}"
        };
    }

    private static string GenerateGroupKey(Activity activity, AggregationOptions options)
    {
        var parts = new List<string>();

        if (options.GroupByActor)
            parts.Add(activity.ActorId.ToString());

        if (options.GroupByResource)
            parts.Add($"{activity.ResourceType}:{activity.ResourceId}");

        if (options.GroupByVerb)
            parts.Add(activity.Verb.ToString());

        // Add time window
        var window = activity.OccurredAt.Ticks / options.GroupWindow.Ticks;
        parts.Add(window.ToString());

        return string.Join("|", parts);
    }

    private static string GenerateGroupSummary(List<Activity> activities, AggregationOptions options)
    {
        var first = activities.First();
        var count = activities.Count;

        if (count == 1)
            return first.Description;

        if (options.GroupByActor && options.GroupByVerb)
        {
            var verb = first.Verb.ToString().ToLower();
            return $"{first.ActorName} {verb} {count} items";
        }

        if (options.GroupByResource)
        {
            return $"{count} activities on {first.ResourceName ?? first.ResourceType?.ToString() ?? "resource"}";
        }

        return $"{count} activities";
    }
}
