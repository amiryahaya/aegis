using System.Reflection;

namespace Aegis.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Aegis.Domain.Common.Entity).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Aegis.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        // Arrange & Act
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Aegis.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames != null
            ? string.Join(", ", result.FailingTypeNames)
            : string.Empty);
    }

    [Fact]
    public void Domain_ShouldNotDependOnApi()
    {
        // Arrange & Act
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Aegis.Api")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames != null
            ? string.Join(", ", result.FailingTypeNames)
            : string.Empty);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("Aegis.Api")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames != null
            ? string.Join(", ", result.FailingTypeNames)
            : string.Empty);
    }
}
