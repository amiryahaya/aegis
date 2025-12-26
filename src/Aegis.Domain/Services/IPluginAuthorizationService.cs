using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for authorizing plugin execution based on user permissions
/// </summary>
public interface IPluginAuthorizationService
{
    /// <summary>
    /// Checks if a user is authorized to execute a specific plugin
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="pluginName">Plugin name</param>
    /// <param name="workspaceId">Workspace ID (optional)</param>
    /// <returns>Authorization result</returns>
    Task<Result<PluginAuthorizationResult>> AuthorizePluginAsync(
        Guid userId,
        string pluginName,
        Guid? workspaceId = null);

    /// <summary>
    /// Gets all plugins available to a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="workspaceId">Workspace ID (optional)</param>
    /// <returns>List of available plugins</returns>
    Task<Result<List<string>>> GetAvailablePluginsAsync(
        Guid userId,
        Guid? workspaceId = null);
}

/// <summary>
/// Result of plugin authorization check
/// </summary>
public record PluginAuthorizationResult
{
    public required bool IsAuthorized { get; init; }
    public required string PluginName { get; init; }
    public string? DenialReason { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}
