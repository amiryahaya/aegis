using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.FeatureFlags;

public class FeatureFlagModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feature-flags")
            .WithTags("Feature Flags");

        group.MapPost("/", CreateFeatureFlag)
            .WithSummary("Create a new feature flag");

        group.MapGet("/{id:guid}", GetFeatureFlagById)
            .WithSummary("Get a feature flag by ID");

        group.MapGet("/key/{key}", GetFeatureFlagByKey)
            .WithSummary("Get a feature flag by key");

        group.MapGet("/", ListFeatureFlags)
            .WithSummary("List all feature flags with optional filtering");

        group.MapPut("/{id:guid}", UpdateFeatureFlag)
            .WithSummary("Update a feature flag");

        group.MapDelete("/{id:guid}", DeleteFeatureFlag)
            .WithSummary("Delete a feature flag");

        group.MapPost("/evaluate/{key}", EvaluateFlag)
            .WithSummary("Evaluate a feature flag for a given context");

        group.MapPost("/evaluate/{key}/variant", GetVariant)
            .WithSummary("Get the variant value for a feature flag");

        group.MapGet("/{id:guid}/audit", GetAuditHistory)
            .WithSummary("Get audit history for a feature flag");
    }

    private static async Task<Results<Created<FeatureFlagResponse>, BadRequest<ProblemDetails>>>
        CreateFeatureFlag(
            CreateFeatureFlagApiRequest request,
            IFeatureFlagService featureFlagService)
    {
        var createRequest = new CreateFeatureFlagRequest
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type ?? FeatureFlagType.Boolean,
            IsEnabled = request.IsEnabled ?? false,
            DefaultValue = request.DefaultValue,
            Rules = request.Rules?.Select(r => new FeatureFlagRule
            {
                Name = r.Name,
                Priority = r.Priority,
                Conditions = r.Conditions?.Select(c => new FeatureFlagCondition
                {
                    Attribute = c.Attribute,
                    Operator = c.Operator,
                    Value = c.Value
                }).ToList() ?? new List<FeatureFlagCondition>(),
                Action = r.Action,
                VariantKey = r.VariantKey,
                RolloutPercentage = r.RolloutPercentage
            }).ToList() ?? new List<FeatureFlagRule>(),
            Variants = request.Variants?.Select(v => new FeatureFlagVariant
            {
                Key = v.Key,
                Name = v.Name,
                Value = v.Value,
                Weight = v.Weight ?? 1
            }).ToList() ?? new List<FeatureFlagVariant>()
        };

        var result = await featureFlagService.CreateAsync(createRequest);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        var response = MapToResponse(result.Value);
        return TypedResults.Created($"/api/feature-flags/{response.Id}", response);
    }

    private static async Task<Results<Ok<FeatureFlagResponse>, NotFound>> GetFeatureFlagById(
        Guid id,
        IFeatureFlagService featureFlagService)
    {
        var result = await featureFlagService.GetByIdAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok<FeatureFlagResponse>, NotFound>> GetFeatureFlagByKey(
        string key,
        IFeatureFlagService featureFlagService)
    {
        var result = await featureFlagService.GetByKeyAsync(key);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Ok<List<FeatureFlagResponse>>> ListFeatureFlags(
        [FromQuery] bool? isEnabled,
        [FromQuery] FeatureFlagType? type,
        IFeatureFlagService featureFlagService)
    {
        var filter = new FeatureFlagFilter
        {
            IsEnabled = isEnabled,
            Type = type
        };

        var result = await featureFlagService.ListAsync(filter);

        var response = result.Value.Select(MapToResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<FeatureFlagResponse>, NotFound, BadRequest<ProblemDetails>>>
        UpdateFeatureFlag(
            Guid id,
            UpdateFeatureFlagApiRequest request,
            IFeatureFlagService featureFlagService)
    {
        var updateRequest = new UpdateFeatureFlagRequest
        {
            Name = request.Name,
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            DefaultValue = request.DefaultValue,
            Rules = request.Rules?.Select(r => new FeatureFlagRule
            {
                Name = r.Name,
                Priority = r.Priority,
                Conditions = r.Conditions?.Select(c => new FeatureFlagCondition
                {
                    Attribute = c.Attribute,
                    Operator = c.Operator,
                    Value = c.Value
                }).ToList() ?? new List<FeatureFlagCondition>(),
                Action = r.Action,
                VariantKey = r.VariantKey,
                RolloutPercentage = r.RolloutPercentage
            }).ToList(),
            Variants = request.Variants?.Select(v => new FeatureFlagVariant
            {
                Key = v.Key,
                Name = v.Name,
                Value = v.Value,
                Weight = v.Weight ?? 1
            }).ToList()
        };

        var result = await featureFlagService.UpdateAsync(id, updateRequest);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("NotFound"))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteFeatureFlag(
        Guid id,
        IFeatureFlagService featureFlagService)
    {
        var result = await featureFlagService.DeleteAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<FlagEvaluationResponse>> EvaluateFlag(
        string key,
        EvaluationContextRequest? request,
        IFeatureFlagService featureFlagService)
    {
        var context = request != null
            ? new FeatureFlagContext
            {
                UserId = request.UserId,
                TeamId = request.TeamId,
                WorkspaceId = request.WorkspaceId,
                Attributes = request.Attributes ?? new Dictionary<string, string>()
            }
            : FeatureFlagContext.Empty;

        var result = await featureFlagService.IsEnabledAsync(key, context);

        return TypedResults.Ok(new FlagEvaluationResponse(
            key,
            result.Value,
            context.UserId,
            context.TeamId));
    }

    private static async Task<Ok<VariantResponse>> GetVariant(
        string key,
        GetVariantRequest request,
        IFeatureFlagService featureFlagService)
    {
        var context = new FeatureFlagContext
        {
            UserId = request.UserId,
            TeamId = request.TeamId,
            WorkspaceId = request.WorkspaceId,
            Attributes = request.Attributes ?? new Dictionary<string, string>()
        };

        var result = await featureFlagService.GetVariantAsync<object>(key, request.DefaultValue, context);

        return TypedResults.Ok(new VariantResponse(
            key,
            result.Value,
            context.UserId,
            context.TeamId));
    }

    private static async Task<Results<Ok<List<FlagAuditEntryResponse>>, NotFound>> GetAuditHistory(
        Guid id,
        [FromQuery] int? limit,
        IFeatureFlagService featureFlagService)
    {
        var result = await featureFlagService.GetAuditHistoryAsync(id, limit ?? 50);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var response = result.Value.Select(e => new FlagAuditEntryResponse(
            e.Id,
            e.FlagId,
            e.Action.ToString(),
            e.OldValue,
            e.NewValue,
            e.ChangedBy,
            e.Reason,
            e.ChangedAt)).ToList();

        return TypedResults.Ok(response);
    }

    private static FeatureFlagResponse MapToResponse(FeatureFlag flag)
    {
        return new FeatureFlagResponse(
            flag.Id,
            flag.Key,
            flag.Name,
            flag.Description,
            flag.Type.ToString(),
            flag.IsEnabled,
            flag.DefaultValue,
            flag.Rules.Select(r => new RuleResponse(
                r.Name,
                r.Priority,
                r.Conditions.Select(c => new ConditionResponse(
                    c.Attribute,
                    c.Operator.ToString(),
                    c.Value?.ToString() ?? string.Empty)).ToList(),
                r.Action.ToString(),
                r.VariantKey,
                r.RolloutPercentage)).ToList(),
            flag.Variants.Select(v => new VariantDefinitionResponse(
                v.Key,
                v.Name,
                v.Value,
                v.Weight)).ToList(),
            flag.CreatedAt,
            flag.UpdatedAt);
    }
}

#region Request/Response DTOs

public record CreateFeatureFlagApiRequest(
    string Key,
    string Name,
    string? Description = null,
    FeatureFlagType? Type = null,
    bool? IsEnabled = null,
    object? DefaultValue = null,
    List<RuleRequest>? Rules = null,
    List<VariantRequest>? Variants = null);

public record UpdateFeatureFlagApiRequest(
    string? Name = null,
    string? Description = null,
    bool? IsEnabled = null,
    object? DefaultValue = null,
    List<RuleRequest>? Rules = null,
    List<VariantRequest>? Variants = null);

public record RuleRequest(
    string Name,
    int Priority,
    List<ConditionRequest>? Conditions,
    FeatureFlagRuleAction Action,
    string? VariantKey = null,
    int? RolloutPercentage = null);

public record ConditionRequest(
    string Attribute,
    FeatureFlagOperator Operator,
    string Value);

public record VariantRequest(
    string Key,
    string Name,
    object Value,
    int? Weight = null);

public record EvaluationContextRequest(
    Guid? UserId = null,
    Guid? TeamId = null,
    Guid? WorkspaceId = null,
    Dictionary<string, string>? Attributes = null);

public record GetVariantRequest(
    object DefaultValue,
    Guid? UserId = null,
    Guid? TeamId = null,
    Guid? WorkspaceId = null,
    Dictionary<string, string>? Attributes = null);

public record FeatureFlagResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string Type,
    bool IsEnabled,
    object? DefaultValue,
    List<RuleResponse> Rules,
    List<VariantDefinitionResponse> Variants,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record RuleResponse(
    string Name,
    int Priority,
    List<ConditionResponse> Conditions,
    string Action,
    string? VariantKey,
    int? RolloutPercentage);

public record ConditionResponse(
    string Attribute,
    string Operator,
    string Value);

public record VariantDefinitionResponse(
    string Key,
    string Name,
    object Value,
    int Weight);

public record FlagEvaluationResponse(
    string Key,
    bool IsEnabled,
    Guid? UserId,
    Guid? TeamId);

public record VariantResponse(
    string Key,
    object? Value,
    Guid? UserId,
    Guid? TeamId);

public record FlagAuditEntryResponse(
    Guid Id,
    Guid FlagId,
    string Action,
    string? OldValue,
    string? NewValue,
    Guid? ChangedBy,
    string? Reason,
    DateTime ChangedAt);

#endregion
