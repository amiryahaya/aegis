using Aegis.Domain.Entities;

namespace Aegis.Domain.Repositories;

/// <summary>
/// Repository for managing feedback
/// </summary>
public interface IFeedbackRepository
{
    Task<Feedback?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Feedback?> GetByQueryHistoryIdAsync(Guid queryHistoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Feedback>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Feedback>> GetByUserIdAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default);
    Task<FeedbackStats> GetStatsAsync(Guid workspaceId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task AddAsync(Feedback feedback, CancellationToken cancellationToken = default);
    Task UpdateAsync(Feedback feedback, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Feedback statistics
/// </summary>
public record FeedbackStats(
    int TotalFeedback,
    int PositiveFeedback,
    int NegativeFeedback,
    int NeutralFeedback,
    double PositiveRate,
    int TotalWithComments);
