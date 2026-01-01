using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Configuration;

public class ConfigurationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var configGroup = app.MapGroup("/api/configuration")
            .WithTags("Configuration");

        configGroup.MapGet("/", GetAllConfiguration)
            .WithSummary("Get all configuration entries");

        configGroup.MapGet("/{key}", GetConfiguration)
            .WithSummary("Get a configuration value by key");

        configGroup.MapGet("/category/{category}", GetConfigurationByCategory)
            .WithSummary("Get all configuration entries in a category");

        configGroup.MapPut("/{key}", SetConfiguration)
            .WithSummary("Set a configuration value");

        configGroup.MapDelete("/{key}", DeleteConfiguration)
            .WithSummary("Delete a configuration entry");

        configGroup.MapPost("/category/{category}/reset", ResetCategory)
            .WithSummary("Reset a configuration category to defaults");

        configGroup.MapPost("/import", ImportConfiguration)
            .WithSummary("Import configuration from a dictionary");

        configGroup.MapGet("/export", ExportConfiguration)
            .WithSummary("Export configuration as a dictionary");

        configGroup.MapGet("/audit", GetConfigurationAudit)
            .WithSummary("Get configuration change audit history");

        configGroup.MapGet("/categories", GetCategories)
            .WithSummary("Get available configuration categories");

        var prefsGroup = app.MapGroup("/api/preferences")
            .WithTags("User Preferences");

        prefsGroup.MapGet("/user/{userId:guid}", GetUserPreferences)
            .WithSummary("Get all preferences for a user");

        prefsGroup.MapGet("/user/{userId:guid}/{key}", GetUserPreference)
            .WithSummary("Get a specific preference for a user");

        prefsGroup.MapPut("/user/{userId:guid}/{key}", SetUserPreference)
            .WithSummary("Set a preference for a user");

        prefsGroup.MapPut("/user/{userId:guid}", UpdateUserPreferences)
            .WithSummary("Update multiple preferences for a user");

        prefsGroup.MapDelete("/user/{userId:guid}/{key}", DeleteUserPreference)
            .WithSummary("Delete a preference for a user");

        prefsGroup.MapPost("/user/{userId:guid}/reset", ResetUserPreferences)
            .WithSummary("Reset a user's preferences to defaults");

        prefsGroup.MapGet("/workspace/{workspaceId:guid}", GetWorkspacePreferences)
            .WithSummary("Get all preferences for a workspace");

        prefsGroup.MapPut("/workspace/{workspaceId:guid}/{key}", SetWorkspacePreference)
            .WithSummary("Set a preference for a workspace");

        prefsGroup.MapPut("/workspace/{workspaceId:guid}", UpdateWorkspacePreferences)
            .WithSummary("Update multiple preferences for a workspace");

        prefsGroup.MapGet("/effective/{userId:guid}", GetEffectivePreferences)
            .WithSummary("Get effective preferences for a user with optional workspace context");
    }

    // Configuration endpoints
    private static async Task<Ok<List<ConfigurationEntryResponse>>> GetAllConfiguration(
        IConfigurationService configurationService)
    {
        var result = await configurationService.GetAllAsync();

        var response = result.Value.Select(MapToConfigResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<ConfigurationValueResponse>> GetConfiguration(
        string key,
        IConfigurationService configurationService)
    {
        var result = await configurationService.GetAsync<object>(key, null!);

        return TypedResults.Ok(new ConfigurationValueResponse(key, result.Value));
    }

    private static async Task<Ok<List<ConfigurationEntryResponse>>> GetConfigurationByCategory(
        ConfigurationCategory category,
        IConfigurationService configurationService)
    {
        var result = await configurationService.GetByCategoryAsync(category);

        var response = result.Value.Select(MapToConfigResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok, BadRequest<ProblemDetails>>> SetConfiguration(
        string key,
        SetConfigurationRequest request,
        IConfigurationService configurationService)
    {
        var result = await configurationService.SetAsync(key, request.Value, request.ChangedBy, request.Reason);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteConfiguration(
        string key,
        IConfigurationService configurationService)
    {
        var result = await configurationService.DeleteAsync(key);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok> ResetCategory(
        ConfigurationCategory category,
        IConfigurationService configurationService)
    {
        await configurationService.ResetCategoryAsync(category);
        return TypedResults.Ok();
    }

    private static async Task<Ok<ImportResultResponse>> ImportConfiguration(
        ImportConfigurationRequest request,
        IConfigurationService configurationService)
    {
        var result = await configurationService.ImportAsync(request.Configuration, request.Overwrite ?? true);

        return TypedResults.Ok(new ImportResultResponse(result.Value));
    }

    private static async Task<Ok<Dictionary<string, object>>> ExportConfiguration(
        [FromQuery] ConfigurationCategory? category,
        IConfigurationService configurationService)
    {
        var result = await configurationService.ExportAsync(category);
        return TypedResults.Ok(result.Value);
    }

    private static async Task<Ok<List<ConfigurationAuditResponse>>> GetConfigurationAudit(
        [FromQuery] string? key,
        [FromQuery] ConfigurationCategory? category,
        [FromQuery] int? limit,
        IConfigurationService configurationService)
    {
        var result = await configurationService.GetAuditHistoryAsync(key, category, limit ?? 50);

        var response = result.Value.Select(e => new ConfigurationAuditResponse(
            e.Key,
            e.Category,
            e.OldValue,
            e.NewValue,
            e.ChangedBy,
            e.Reason,
            e.ChangedAt)).ToList();

        return TypedResults.Ok(response);
    }

    private static Ok<List<CategoryInfo>> GetCategories()
    {
        var categories = Enum.GetValues<ConfigurationCategory>()
            .Select(c => new CategoryInfo(c.ToString(), GetCategoryDescription(c)))
            .ToList();

        return TypedResults.Ok(categories);
    }

    // User preferences endpoints
    private static async Task<Ok<UserPreferencesResponse>> GetUserPreferences(
        Guid userId,
        IUserPreferencesService preferencesService)
    {
        var result = await preferencesService.GetAllAsync(userId);
        return TypedResults.Ok(MapToUserPrefsResponse(result.Value));
    }

    private static async Task<Ok<PreferenceValueResponse>> GetUserPreference(
        Guid userId,
        string key,
        IUserPreferencesService preferencesService)
    {
        var result = await preferencesService.GetAsync<object>(userId, key, null!);
        return TypedResults.Ok(new PreferenceValueResponse(key, result.Value));
    }

    private static async Task<Ok> SetUserPreference(
        Guid userId,
        string key,
        SetPreferenceRequest request,
        IUserPreferencesService preferencesService)
    {
        await preferencesService.SetAsync(userId, key, request.Value);
        return TypedResults.Ok();
    }

    private static async Task<Ok> UpdateUserPreferences(
        Guid userId,
        UpdatePreferencesRequest request,
        IUserPreferencesService preferencesService)
    {
        await preferencesService.UpdateAsync(userId, request.Preferences);
        return TypedResults.Ok();
    }

    private static async Task<Ok> DeleteUserPreference(
        Guid userId,
        string key,
        IUserPreferencesService preferencesService)
    {
        await preferencesService.DeleteAsync(userId, key);
        return TypedResults.Ok();
    }

    private static async Task<Ok> ResetUserPreferences(
        Guid userId,
        IUserPreferencesService preferencesService)
    {
        await preferencesService.ResetToDefaultsAsync(userId);
        return TypedResults.Ok();
    }

    private static async Task<Ok<WorkspacePreferencesResponse>> GetWorkspacePreferences(
        Guid workspaceId,
        IUserPreferencesService preferencesService)
    {
        var result = await preferencesService.GetAllWorkspaceAsync(workspaceId);
        return TypedResults.Ok(MapToWorkspacePrefsResponse(result.Value));
    }

    private static async Task<Ok> SetWorkspacePreference(
        Guid workspaceId,
        string key,
        SetPreferenceRequest request,
        IUserPreferencesService preferencesService)
    {
        await preferencesService.SetWorkspaceAsync(workspaceId, key, request.Value);
        return TypedResults.Ok();
    }

    private static async Task<Ok> UpdateWorkspacePreferences(
        Guid workspaceId,
        UpdatePreferencesRequest request,
        IUserPreferencesService preferencesService)
    {
        await preferencesService.UpdateWorkspaceAsync(workspaceId, request.Preferences);
        return TypedResults.Ok();
    }

    private static async Task<Ok<EffectivePreferencesResponse>> GetEffectivePreferences(
        Guid userId,
        [FromQuery] Guid? workspaceId,
        IUserPreferencesService preferencesService)
    {
        var userPrefs = await preferencesService.GetAllAsync(userId);
        WorkspacePreferences? workspacePrefs = null;

        if (workspaceId.HasValue)
        {
            var wsResult = await preferencesService.GetAllWorkspaceAsync(workspaceId.Value);
            workspacePrefs = wsResult.Value;
        }

        return TypedResults.Ok(new EffectivePreferencesResponse(
            userId,
            workspaceId,
            MapToUserPrefsResponse(userPrefs.Value),
            workspacePrefs != null ? MapToWorkspacePrefsResponse(workspacePrefs) : null));
    }

    private static ConfigurationEntryResponse MapToConfigResponse(ConfigurationEntry entry)
    {
        return new ConfigurationEntryResponse(
            entry.Key,
            entry.Category.ToString(),
            entry.Value,
            entry.Description,
            entry.IsSecret,
            entry.UpdatedAt);
    }

    private static UserPreferencesResponse MapToUserPrefsResponse(UserPreferences prefs)
    {
        return new UserPreferencesResponse(
            prefs.UserId,
            new UiPreferencesResponse(
                prefs.Ui.Theme,
                prefs.Locale.Language,
                prefs.Ui.CompactMode,
                prefs.Ui.SidebarCollapsed),
            new QueryPreferencesResponse(
                prefs.Query.DefaultModel,
                prefs.Query.Temperature,
                prefs.Query.MaxResults,
                prefs.Query.EnableCaching),
            new NotificationPreferencesResponse(
                prefs.Notifications.EmailEnabled,
                prefs.Notifications.InAppEnabled,
                prefs.Notifications.DigestFrequency.ToString()),
            prefs.Custom);
    }

    private static WorkspacePreferencesResponse MapToWorkspacePrefsResponse(WorkspacePreferences prefs)
    {
        return new WorkspacePreferencesResponse(
            prefs.WorkspaceId,
            new DataPreferencesResponse(
                prefs.Data.ChunkSize,
                prefs.Data.ChunkOverlap,
                prefs.Data.RetentionDays,
                prefs.Data.EnableVersioning),
            new QueryPreferencesResponse(
                prefs.Query.DefaultModel,
                prefs.Query.Temperature,
                prefs.Query.MaxResults,
                prefs.Query.EnableCaching),
            prefs.Custom);
    }

    private static string GetCategoryDescription(ConfigurationCategory category) => category switch
    {
        ConfigurationCategory.System => "System-wide settings",
        ConfigurationCategory.Llm => "Language model configuration",
        ConfigurationCategory.Cache => "Caching settings",
        ConfigurationCategory.RateLimit => "Rate limiting configuration",
        ConfigurationCategory.Webhook => "Webhook settings",
        ConfigurationCategory.Security => "Security and authentication settings",
        ConfigurationCategory.Storage => "Storage configuration",
        ConfigurationCategory.Search => "Search and indexing settings",
        ConfigurationCategory.Observability => "Logging and monitoring settings",
        _ => "Unknown category"
    };
}

#region Request/Response DTOs

public record SetConfigurationRequest(
    object Value,
    Guid? ChangedBy = null,
    string? Reason = null);

public record ImportConfigurationRequest(
    Dictionary<string, object> Configuration,
    bool? Overwrite = null);

public record SetPreferenceRequest(object Value);

public record UpdatePreferencesRequest(Dictionary<string, object> Preferences);

public record ConfigurationEntryResponse(
    string Key,
    string Category,
    object Value,
    string? Description,
    bool IsSecret,
    DateTime? UpdatedAt);

public record ConfigurationValueResponse(string Key, object? Value);

public record ImportResultResponse(int ImportedCount);

public record ConfigurationAuditResponse(
    string Key,
    ConfigurationCategory Category,
    string? OldValue,
    string? NewValue,
    Guid? ChangedBy,
    string? Reason,
    DateTime ChangedAt);

public record CategoryInfo(string Name, string Description);

public record PreferenceValueResponse(string Key, object? Value);

public record UserPreferencesResponse(
    Guid UserId,
    UiPreferencesResponse Ui,
    QueryPreferencesResponse Query,
    NotificationPreferencesResponse Notifications,
    Dictionary<string, object> Custom);

public record UiPreferencesResponse(
    string Theme,
    string Language,
    bool CompactMode,
    bool SidebarCollapsed);

public record QueryPreferencesResponse(
    string DefaultModel,
    double Temperature,
    int MaxResults,
    bool EnableCaching);

public record NotificationPreferencesResponse(
    bool EmailEnabled,
    bool InAppEnabled,
    string DigestFrequency);

public record WorkspacePreferencesResponse(
    Guid WorkspaceId,
    DataPreferencesResponse Data,
    QueryPreferencesResponse Query,
    Dictionary<string, object> Custom);

public record DataPreferencesResponse(
    int ChunkSize,
    int ChunkOverlap,
    int RetentionDays,
    bool EnableVersioning);

public record EffectivePreferencesResponse(
    Guid UserId,
    Guid? WorkspaceId,
    UserPreferencesResponse UserPreferences,
    WorkspacePreferencesResponse? WorkspacePreferences);

#endregion
