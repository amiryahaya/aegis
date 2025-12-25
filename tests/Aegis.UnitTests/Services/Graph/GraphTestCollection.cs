namespace Aegis.UnitTests.Services.Graph;

/// <summary>
/// xUnit collection definition to prevent parallel execution of graph tests.
/// This ensures test isolation and prevents interference between tests accessing Neo4j.
/// </summary>
[CollectionDefinition("GraphTests", DisableParallelization = true)]
public class GraphTestCollection
{
    // This class is never instantiated. It exists only to define the collection.
}
