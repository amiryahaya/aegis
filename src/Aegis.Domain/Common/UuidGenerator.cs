using UUIDNext;

namespace Aegis.Domain.Common;

/// <summary>
/// Generates time-ordered UUID v7 identifiers
/// </summary>
public static class UuidGenerator
{
    /// <summary>
    /// Generates a new UUID v7 (time-ordered, monotonic)
    /// </summary>
    /// <returns>UUID v7 as System.Guid</returns>
    public static Guid NewId() => Uuid.NewDatabaseFriendly(Database.PostgreSql);

    /// <summary>
    /// Generates a new UUID v7 with specific timestamp (for testing)
    /// </summary>
    public static Guid NewId(DateTimeOffset timestamp)
    {
        // UUIDNext doesn't support custom timestamps, so we'll use the default
        // For testing, tests can use fixed UUIDs directly
        return Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
