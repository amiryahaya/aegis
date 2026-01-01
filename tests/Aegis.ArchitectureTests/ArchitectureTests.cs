using System.Reflection;

namespace Aegis.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Aegis.Domain.Common.Entity).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Aegis.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    #region Layer Dependency Tests

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

    [Fact]
    public void Infrastructure_ShouldReferencesDomain()
    {
        // Verify that Infrastructure assembly references Domain assembly
        var infrastructureReferences = InfrastructureAssembly.GetReferencedAssemblies();
        var hasDomainReference = infrastructureReferences.Any(r => r.Name == "Aegis.Domain");

        hasDomainReference.Should().BeTrue(
            "Infrastructure should reference Domain assembly");
    }

    #endregion

    #region Naming Convention Tests

    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        // Arrange & Act
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"All interfaces should start with 'I'. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void ServiceInterfaces_InDomain_ShouldStartWithI()
    {
        // Arrange & Act - verify interfaces in Domain.Services follow naming conventions
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Aegis.Domain.Services")
            .And()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Interfaces in Domain.Services should start with 'I'. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void Repositories_ShouldEndWithRepository()
    {
        // Arrange & Act
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Aegis.Domain.Repositories")
            .And()
            .AreInterfaces()
            .Should()
            .HaveNameEndingWith("Repository")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Repository interfaces should end with 'Repository'. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void Entities_ShouldResideInEntitiesOrCommonNamespace()
    {
        // Arrange & Act
        // Entity classes can be in Domain.Entities or Domain.Common (for base classes like AggregateRoot)
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(Aegis.Domain.Common.Entity))
            .And()
            .DoNotResideInNamespace("Aegis.Domain.Common") // Exclude base classes
            .Should()
            .ResideInNamespace("Aegis.Domain.Entities")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Entities should reside in Domain.Entities namespace. Violating types: {GetFailingTypes(result)}");
    }

    #endregion

    #region Implementation Convention Tests

    [Fact]
    public void ServiceImplementations_ShouldBeInInfrastructure()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Service")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Services")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Service implementations should reside in Infrastructure.Services. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void RepositoryImplementations_ShouldBeInInfrastructure()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Repository implementations should reside in Infrastructure. Violating types: {GetFailingTypes(result)}");
    }

    #endregion

    #region No Circular Dependencies Tests

    [Fact]
    public void Entities_ShouldNotDependOnServices()
    {
        // Arrange & Act
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Aegis.Domain.Entities")
            .ShouldNot()
            .HaveDependencyOn("Aegis.Domain.Services")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Entities should not depend on Services. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void Entities_ShouldNotDependOnRepositories()
    {
        // Arrange & Act
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Aegis.Domain.Entities")
            .ShouldNot()
            .HaveDependencyOn("Aegis.Domain.Repositories")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Entities should not depend on Repositories. Violating types: {GetFailingTypes(result)}");
    }

    #endregion

    #region Controller Tests

    [Fact]
    public void Controllers_ShouldEndWithController()
    {
        // Arrange & Act
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
            .Should()
            .HaveNameEndingWith("Controller")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Controllers should end with 'Controller'. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void Controllers_ShouldResideInControllersNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Api.Controllers")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Controllers should reside in Api.Controllers namespace. Violating types: {GetFailingTypes(result)}");
    }

    #endregion

    #region Plugin and Agent Tests

    [Fact]
    public void Plugins_ShouldResideInPluginsNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Plugin")
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Plugins")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Plugins should reside in Infrastructure.Plugins namespace. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void Agents_ShouldResideInAgentsNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Agent")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Services.Agents")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Agents should reside in Infrastructure.Services.Agents. Violating types: {GetFailingTypes(result)}");
    }

    #endregion

    #region Security Tests

    [Fact]
    public void SecurityServices_ShouldResideInSecurityNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameMatching(".*Sanitizer|.*Filter|.*RateLimiter|.*ApiKeyService")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Services.Security")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Security services should reside in Infrastructure.Services.Security. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void CachingServices_ShouldResideInCachingNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameMatching(".*Cache$")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Services.Caching")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Caching services should reside in Infrastructure.Services.Caching. Violating types: {GetFailingTypes(result)}");
    }

    [Fact]
    public void AdminServices_ShouldResideInAdminNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameMatching(".*AuditLog.*|.*Analytics.*|.*Exporter.*|.*Dashboard.*")
            .And()
            .AreClasses()
            .And()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Services")
            .Should()
            .ResideInNamespaceStartingWith("Aegis.Infrastructure.Services.Admin")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"Admin services should reside in Infrastructure.Services.Admin. Violating types: {GetFailingTypes(result)}");
    }

    #endregion

    #region Domain Interface Pattern Tests

    [Fact]
    public void DomainServices_ShouldHaveCorrespondingInterface()
    {
        // This test verifies the pattern that domain services are interfaces
        var interfaces = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Aegis.Domain.Services")
            .And()
            .AreInterfaces()
            .GetTypes();

        // There should be domain service interfaces
        interfaces.Should().NotBeEmpty("Domain.Services should contain service interfaces");
    }

    [Fact]
    public void Repositories_ShouldHaveCorrespondingInterface()
    {
        // This test verifies the pattern that repositories are interfaces in domain
        var interfaces = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Aegis.Domain.Repositories")
            .And()
            .AreInterfaces()
            .GetTypes();

        // There should be repository interfaces
        interfaces.Should().NotBeEmpty("Domain.Repositories should contain repository interfaces");
    }

    #endregion

    #region Helper Methods

    private static string GetFailingTypes(TestResult result)
    {
        if (result.FailingTypeNames == null || !result.FailingTypeNames.Any())
            return "None";

        return string.Join(", ", result.FailingTypeNames);
    }

    #endregion
}
