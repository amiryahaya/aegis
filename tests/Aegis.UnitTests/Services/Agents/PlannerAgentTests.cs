using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class PlannerAgentTests
{
    private readonly ILogger<PlannerAgent> _logger;
    private readonly Kernel _kernel;

    public PlannerAgentTests()
    {
        _logger = Substitute.For<ILogger<PlannerAgent>>();

        // Create a minimal kernel for testing
        var builder = Kernel.CreateBuilder();
        _kernel = builder.Build();
    }

    [Fact]
    public async Task CreatePlanAsync_WithSimpleQuery_ShouldReturnSingleTaskPlan()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);
        var query = "What is the capital of France?";
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await agent.CreatePlanAsync(query, workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Query.Should().Be(query);
        result.Value.Tasks.Should().NotBeEmpty();
        result.Value.Tasks.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task CreatePlanAsync_WithComplexQuery_ShouldReturnMultiStepPlan()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);
        var query = "Find all transactions for John Doe in 2024 and analyze the patterns";
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await agent.CreatePlanAsync(query, workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Tasks.Should().NotBeEmpty();

        // Should have multiple tasks for a complex query
        result.Value.Tasks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task CreatePlanAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await agent.CreatePlanAsync("", workspaceId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ShouldReturnPlan()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);
        var request = new AgentRequest
        {
            RequestId = Guid.NewGuid(),
            Task = "Find information about XYZ Corporation",
            WorkspaceId = Guid.NewGuid()
        };

        // Act
        var result = await agent.ExecuteAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Result.Should().NotBeEmpty();
    }

    [Fact]
    public void AgentType_ShouldReturnPlanner()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);

        // Act
        var agentType = agent.AgentType;

        // Assert
        agentType.Should().Be("Planner");
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldAssignTaskIds()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);
        var query = "Search for documents and analyze them";
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await agent.CreatePlanAsync(query, workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tasks.Should().AllSatisfy(task =>
        {
            task.TaskId.Should().NotBe(Guid.Empty);
            task.Description.Should().NotBeEmpty();
            task.AgentType.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldIncludeWorkspaceId()
    {
        // Arrange
        var agent = new PlannerAgent(_kernel, _logger);
        var query = "Find entities related to John Doe";
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await agent.CreatePlanAsync(query, workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Check that workspace context is included in tasks
        result.Value.Tasks.Should().AllSatisfy(task =>
        {
            task.Parameters.Should().ContainKey("workspaceId");
            task.Parameters["workspaceId"].Should().Be(workspaceId);
        });
    }
}
