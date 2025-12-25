using System.Net;
using System.Net.Http.Json;
using Aegis.Api.Features.Knowledge;
using Aegis.Api.Features.Workspaces;
using Aegis.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Aegis.IntegrationTests.Workflows;

[Collection("Integration")]
public class WorkspaceWorkflowTests
{
    private readonly HttpClient _client;

    public WorkspaceWorkflowTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteWorkspaceWorkflow_ShouldWork()
    {
        // Step 1: Create a test user
        var userId = await CreateTestUserAsync();

        // Step 2: Create a workspace
        var workspaceId = await CreateWorkspaceAsync(userId, "Intelligence Analysis Workspace");

        // Step 3: Add entities to the knowledge base
        var entity1Id = await AddEntityAsync(workspaceId, userId, "John Doe", "Person", "CEO of Acme Corp");
        var entity2Id = await AddEntityAsync(workspaceId, userId, "Acme Corp", "Organization", "Technology company");

        // Step 4: Add findings
        var findingId = await AddFindingAsync(workspaceId, userId,
            "Leadership Connection",
            "John Doe is the CEO of Acme Corp, established through multiple sources.",
            "Evidence",
            new List<Guid> { entity1Id, entity2Id });

        // Step 5: Add facts
        var factId = await AddFactAsync(workspaceId, userId,
            "Acme Corp was founded in 2010",
            "Confirmed");

        // Step 6: Get full workspace context
        var context = await GetWorkspaceContextAsync(workspaceId);
        context.Should().NotBeNull();
        context!.Entities.Should().HaveCount(2);
        context.Findings.Should().HaveCount(1);
        context.Facts.Should().HaveCount(1);
        context.FormattedContext.Should().NotBeNullOrEmpty();

        // Step 7: Get relevant context for a query
        var relevantContext = await GetRelevantContextAsync(workspaceId, "Who is the CEO of Acme?");
        relevantContext.Should().NotBeNull();
        relevantContext!.Entities.Should().Contain(e => e.Name == "John Doe");
        relevantContext.Entities.Should().Contain(e => e.Name == "Acme Corp");

        // Step 8: Create conversations in the workspace
        var conversation1Id = await CreateConversationAsync(workspaceId, userId, "Analysis of leadership");
        var conversation2Id = await CreateConversationAsync(workspaceId, userId, "Company background");

        // Step 9: Get all conversations
        var conversations = await GetConversationsAsync(workspaceId);
        conversations.Should().HaveCount(2);

        // Step 10: Update entity confidence
        await UpdateEntityConfidenceAsync(workspaceId, entity1Id, "High");

        // Step 11: Get updated entity
        var updatedEntity = await GetEntityAsync(workspaceId, entity1Id);
        updatedEntity.Should().NotBeNull();
        updatedEntity!.Confidence.Should().Be("High");

        // Step 12: Archive the workspace
        await ArchiveWorkspaceAsync(workspaceId);

        // Step 13: Verify workspace is archived
        var archivedWorkspace = await GetWorkspaceAsync(workspaceId);
        archivedWorkspace.Should().NotBeNull();
        archivedWorkspace!.Status.Should().Be("Archived");
    }

    [Fact]
    public async Task WorkspaceWithCustomInstructions_ShouldIncludeInContext()
    {
        var userId = await CreateTestUserAsync();
        var workspaceId = await CreateWorkspaceAsync(userId, "Custom Instructions Test",
            customInstructions: "Focus on financial analysis and risk assessment.");

        var context = await GetWorkspaceContextAsync(workspaceId);
        context.Should().NotBeNull();
        context!.CustomInstructions.Should().Contain("financial analysis");
        context.FormattedContext.Should().Contain("Custom Instructions");
    }

    [Fact]
    public async Task MultipleEntitiesOfSameType_ShouldBeFilterable()
    {
        var userId = await CreateTestUserAsync();
        var workspaceId = await CreateWorkspaceAsync(userId, "Entity Type Test");

        // Add multiple person entities
        await AddEntityAsync(workspaceId, userId, "Alice Smith", "Person", "CFO");
        await AddEntityAsync(workspaceId, userId, "Bob Jones", "Person", "CTO");
        await AddEntityAsync(workspaceId, userId, "TechCorp", "Organization", "Software company");

        // Get all entities
        var allEntities = await GetEntitiesAsync(workspaceId);
        allEntities.Should().HaveCount(3);

        // Get only person entities
        var personEntities = await GetEntitiesByTypeAsync(workspaceId, "Person");
        personEntities.Should().HaveCount(2);
        personEntities.Should().AllSatisfy(e => e.Type.Should().Be("Person"));
    }

    // ======================== HELPER METHODS ========================

    private async Task<Guid> CreateTestUserAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "Test123!",
            FullName = "Test User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        return Guid.Parse(result!.id.ToString());
    }

    private async Task<Guid> CreateWorkspaceAsync(Guid userId, string name, string? customInstructions = null)
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new
        {
            Name = name,
            CreatedBy = userId,
            Description = "Test workspace",
            CustomInstructions = customInstructions
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = response.Headers.Location!.ToString();
        return Guid.Parse(location.Split('/').Last());
    }

    private async Task<Guid> AddEntityAsync(Guid workspaceId, Guid userId, string name, string type, string? description = null)
    {
        var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/knowledge/entities", new
        {
            Name = name,
            Type = type,
            Description = description,
            AddedBy = userId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        return Guid.Parse(result!.id.ToString());
    }

    private async Task<Guid> AddFindingAsync(Guid workspaceId, Guid userId, string title, string content,
        string type, List<Guid>? supportingEntityIds = null)
    {
        var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/knowledge/findings", new
        {
            Title = title,
            Content = content,
            Type = type,
            AddedBy = userId,
            SupportingEntityIds = supportingEntityIds ?? new List<Guid>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        return Guid.Parse(result!.id.ToString());
    }

    private async Task<Guid> AddFactAsync(Guid workspaceId, Guid userId, string statement, string confidence)
    {
        var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/knowledge/facts", new
        {
            Statement = statement,
            Confidence = confidence,
            AddedBy = userId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        return Guid.Parse(result!.id.ToString());
    }

    private async Task<WorkspaceContextResponse?> GetWorkspaceContextAsync(Guid workspaceId)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<WorkspaceContextResponse>();
    }

    private async Task<WorkspaceContextResponse?> GetRelevantContextAsync(Guid workspaceId, string query)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/context/relevant?query={Uri.EscapeDataString(query)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<WorkspaceContextResponse>();
    }

    private async Task<Guid> CreateConversationAsync(Guid workspaceId, Guid userId, string title)
    {
        var response = await _client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/conversations", new
        {
            Title = title,
            CreatedBy = userId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        return Guid.Parse(result!.id.ToString());
    }

    private async Task<List<dynamic>> GetConversationsAsync(Guid workspaceId)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/conversations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<List<dynamic>>() ?? new List<dynamic>();
    }

    private async Task UpdateEntityConfidenceAsync(Guid workspaceId, Guid entityId, string confidence)
    {
        var response = await _client.PutAsJsonAsync($"/api/workspaces/{workspaceId}/knowledge/entities/{entityId}", new
        {
            Name = "Updated",
            Confidence = confidence
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
    }

    private async Task<EntityDto?> GetEntityAsync(Guid workspaceId, Guid entityId)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/knowledge/entities/{entityId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<EntityDto>();
    }

    private async Task<List<EntityDto>> GetEntitiesAsync(Guid workspaceId)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/knowledge/entities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<List<EntityDto>>() ?? new List<EntityDto>();
    }

    private async Task<List<EntityDto>> GetEntitiesByTypeAsync(Guid workspaceId, string type)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/knowledge/entities?type={Uri.EscapeDataString(type)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<List<EntityDto>>() ?? new List<EntityDto>();
    }

    private async Task ArchiveWorkspaceAsync(Guid workspaceId)
    {
        var response = await _client.PostAsync($"/api/workspaces/{workspaceId}/archive", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<WorkspaceResponse?> GetWorkspaceAsync(Guid workspaceId)
    {
        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
    }
}

// DTOs for deserialization
public record EntityDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Type,
    string? Description,
    List<string> Aliases,
    string Confidence,
    Guid? SourceConversationId,
    Guid AddedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
