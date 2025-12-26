using Aegis.Domain.Common;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Plugin authorization service implementation
/// </summary>
public class PluginAuthorizationService : IPluginAuthorizationService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PluginAuthorizationService> _logger;

    // Define plugin authorization rules
    private static readonly Dictionary<string, string[]> PluginRoleRequirements = new()
    {
        { "VectorSearchPlugin", new[] { "Viewer", "Contributor", "Analyst", "Admin" } },
        { "KeywordSearchPlugin", new[] { "Viewer", "Contributor", "Analyst", "Admin" } },
        { "GraphQueryPlugin", new[] { "Contributor", "Analyst", "Admin" } },
        { "EntityLookupPlugin", new[] { "Contributor", "Analyst", "Admin" } },
        { "SanctionsCheckPlugin", new[] { "Analyst", "Admin" } },
        { "TimelineBuilderPlugin", new[] { "Analyst", "Admin" } }
    };

    public PluginAuthorizationService(
        IUserRepository userRepository,
        ILogger<PluginAuthorizationService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<PluginAuthorizationResult>> AuthorizePluginAsync(
        Guid userId,
        string pluginName,
        Guid? workspaceId = null)
    {
        try
        {
            // Get user
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return Result<PluginAuthorizationResult>.Success(new PluginAuthorizationResult
                {
                    IsAuthorized = false,
                    PluginName = pluginName,
                    DenialReason = "User not found"
                });
            }

            // Check if plugin exists in authorization rules
            if (!PluginRoleRequirements.TryGetValue(pluginName, out var requiredRoles))
            {
                _logger.LogWarning("Unknown plugin: {PluginName}", pluginName);
                return Result<PluginAuthorizationResult>.Success(new PluginAuthorizationResult
                {
                    IsAuthorized = false,
                    PluginName = pluginName,
                    DenialReason = "Unknown plugin"
                });
            }

            // Check if user's role is authorized
            var userRole = user.Role.ToString();
            var isAuthorized = requiredRoles.Contains(userRole);

            _logger.LogInformation(
                "Plugin authorization: User={UserId}, Plugin={PluginName}, Role={Role}, Authorized={Authorized}",
                userId, pluginName, userRole, isAuthorized);

            return Result<PluginAuthorizationResult>.Success(new PluginAuthorizationResult
            {
                IsAuthorized = isAuthorized,
                PluginName = pluginName,
                DenialReason = isAuthorized ? null : $"Role '{userRole}' not authorized for this plugin",
                Metadata = new Dictionary<string, object>
                {
                    { "userRole", userRole },
                    { "requiredRoles", requiredRoles }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing plugin: {PluginName}", pluginName);
            return Result<PluginAuthorizationResult>.Failure(
                Error.Internal("PluginAuth.Error", ex.Message));
        }
    }

    public async Task<Result<List<string>>> GetAvailablePluginsAsync(
        Guid userId,
        Guid? workspaceId = null)
    {
        try
        {
            // Get user
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return Result<List<string>>.Failure(
                    Error.NotFound("User.NotFound", $"User with ID {userId} not found"));
            }
            var userRole = user.Role.ToString();

            // Filter plugins by role
            var availablePlugins = PluginRoleRequirements
                .Where(kvp => kvp.Value.Contains(userRole))
                .Select(kvp => kvp.Key)
                .ToList();

            _logger.LogInformation(
                "Available plugins for user {UserId} (Role={Role}): {Count}",
                userId, userRole, availablePlugins.Count);

            return Result<List<string>>.Success(availablePlugins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available plugins for user {UserId}", userId);
            return Result<List<string>>.Failure(
                Error.Internal("PluginAuth.Error", ex.Message));
        }
    }
}
