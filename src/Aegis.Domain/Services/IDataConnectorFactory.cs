using Aegis.Domain.Common;
using Aegis.Domain.Entities;

namespace Aegis.Domain.Services;

/// <summary>
/// Factory for creating appropriate connector based on DataSourceType
/// </summary>
public interface IDataConnectorFactory
{
    /// <summary>
    /// Gets the appropriate connector for a data source type
    /// </summary>
    Result<IDataConnector> GetConnector(DataSourceType dataSourceType);

    /// <summary>
    /// Gets all available connectors
    /// </summary>
    IReadOnlyList<IDataConnector> GetAllConnectors();
}
