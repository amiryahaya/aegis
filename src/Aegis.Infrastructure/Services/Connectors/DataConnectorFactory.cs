using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Connectors;

/// <summary>
/// Factory for creating data connectors based on data source type
/// </summary>
public class DataConnectorFactory : IDataConnectorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<DataSourceType, Type> _connectorTypes;

    public DataConnectorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _connectorTypes = new Dictionary<DataSourceType, Type>();

        // Register connector types
        // We'll register them dynamically as we create them
    }

    public Result<IDataConnector> GetConnector(DataSourceType dataSourceType)
    {
        if (!_connectorTypes.TryGetValue(dataSourceType, out var connectorType))
        {
            return Result<IDataConnector>.Failure(
                Error.NotFound("Connector.NotFound", $"No connector found for data source type: {dataSourceType}"));
        }

        var connector = _serviceProvider.GetService(connectorType) as IDataConnector;
        if (connector == null)
        {
            return Result<IDataConnector>.Failure(
                Error.Internal("Connector.NotRegistered", $"Connector for {dataSourceType} is not registered in the service container"));
        }

        return Result<IDataConnector>.Success(connector);
    }

    public IReadOnlyList<IDataConnector> GetAllConnectors()
    {
        var connectors = new List<IDataConnector>();

        foreach (var connectorType in _connectorTypes.Values.Distinct())
        {
            var connector = _serviceProvider.GetService(connectorType) as IDataConnector;
            if (connector != null)
            {
                connectors.Add(connector);
            }
        }

        return connectors.AsReadOnly();
    }

    /// <summary>
    /// Register a connector type for a specific data source type
    /// </summary>
    public void RegisterConnector(DataSourceType dataSourceType, Type connectorType)
    {
        if (!typeof(IDataConnector).IsAssignableFrom(connectorType))
        {
            throw new ArgumentException($"Type {connectorType.Name} does not implement IDataConnector", nameof(connectorType));
        }

        _connectorTypes[dataSourceType] = connectorType;
    }
}
