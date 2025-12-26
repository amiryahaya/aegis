using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class TaskExecutorTests
{
    private readonly ILogger<TaskExecutor> _logger;
    private readonly Dictionary<string, IAgent> _agents;

    public TaskExecutorTests()
    {
        _logger = Substitute.For<ILogger<TaskExecutor>>();
        _agents = new Dictionary<string, IAgent>();
    }

    [Fact]
    public async Task ExecutePlanAsync_WithSingleTask_ShouldExecuteSuccessfully()
    {
        // Arrange
        var mockAgent = Substitute.For<IAgent>();
        mockAgent.AgentType.Returns("TestAgent");
        mockAgent.ExecuteAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentResponse>.Success(new AgentResponse
            {
                Result = "Test result",
                Confidence = 0.9
            }));

        _agents["TestAgent"] = mockAgent;

        var executor = new TaskExecutor(_agents, _logger);

        var plan = new ExecutionPlan
        {
            PlanId = Guid.NewGuid(),
            Query = "Test query",
            Tasks = new List<PlanTask>
            {
                new()
                {
                    TaskId = Guid.NewGuid(),
                    Description = "Test task",
                    AgentType = "TestAgent",
                    Parameters = new Dictionary<string, object>()
                }
            }
        };

        // Act
        var result = await executor.ExecutePlanAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.TaskResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecutePlanAsync_WithDependentTasks_ShouldExecuteInOrder()
    {
        // Arrange
        var executionOrder = new List<Guid>();

        var retrieverAgent = Substitute.For<IAgent>();
        retrieverAgent.AgentType.Returns("Retriever");
        retrieverAgent.ExecuteAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<AgentRequest>();
                executionOrder.Add(request.RequestId);
                return Result<AgentResponse>.Success(new AgentResponse
                {
                    Result = "Retrieved data",
                    Confidence = 0.9
                });
            });

        var analyzerAgent = Substitute.For<IAgent>();
        analyzerAgent.AgentType.Returns("Analyzer");
        analyzerAgent.ExecuteAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<AgentRequest>();
                executionOrder.Add(request.RequestId);
                return Result<AgentResponse>.Success(new AgentResponse
                {
                    Result = "Analysis complete",
                    Confidence = 0.85
                });
            });

        _agents["Retriever"] = retrieverAgent;
        _agents["Analyzer"] = analyzerAgent;

        var executor = new TaskExecutor(_agents, _logger);

        var task1Id = Guid.NewGuid();
        var task2Id = Guid.NewGuid();

        var plan = new ExecutionPlan
        {
            PlanId = Guid.NewGuid(),
            Query = "Test query",
            Tasks = new List<PlanTask>
            {
                new()
                {
                    TaskId = task1Id,
                    Description = "Retrieve data",
                    AgentType = "Retriever"
                },
                new()
                {
                    TaskId = task2Id,
                    Description = "Analyze data",
                    AgentType = "Analyzer",
                    Dependencies = new List<Guid> { task1Id }
                }
            }
        };

        // Act
        var result = await executor.ExecutePlanAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        executionOrder.Should().HaveCount(2);
        // First task should execute before second task
        var task1Index = executionOrder.FindIndex(id => id == task1Id);
        var task2Index = executionOrder.FindIndex(id => id == task2Id);
        task1Index.Should().BeLessThan(task2Index);
    }

    [Fact]
    public async Task ExecutePlanAsync_WithMissingAgent_ShouldReturnFailure()
    {
        // Arrange
        var executor = new TaskExecutor(_agents, _logger);

        var plan = new ExecutionPlan
        {
            PlanId = Guid.NewGuid(),
            Query = "Test query",
            Tasks = new List<PlanTask>
            {
                new()
                {
                    TaskId = Guid.NewGuid(),
                    Description = "Test task",
                    AgentType = "NonExistentAgent"
                }
            }
        };

        // Act
        var result = await executor.ExecutePlanAsync(plan);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecutePlanAsync_WithEmptyPlan_ShouldReturnSuccess()
    {
        // Arrange
        var executor = new TaskExecutor(_agents, _logger);

        var plan = new ExecutionPlan
        {
            PlanId = Guid.NewGuid(),
            Query = "Test query",
            Tasks = new List<PlanTask>()
        };

        // Act
        var result = await executor.ExecutePlanAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TaskResults.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithValidTask_ShouldReturnResult()
    {
        // Arrange
        var mockAgent = Substitute.For<IAgent>();
        mockAgent.AgentType.Returns("TestAgent");
        mockAgent.ExecuteAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentResponse>.Success(new AgentResponse
            {
                Result = "Task completed",
                Confidence = 0.95
            }));

        _agents["TestAgent"] = mockAgent;

        var executor = new TaskExecutor(_agents, _logger);

        var task = new PlanTask
        {
            TaskId = Guid.NewGuid(),
            Description = "Test task",
            AgentType = "TestAgent"
        };

        // Act
        var result = await executor.ExecuteTaskAsync(task, new Dictionary<Guid, TaskResult>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.Output.Should().NotBeEmpty();
    }
}
