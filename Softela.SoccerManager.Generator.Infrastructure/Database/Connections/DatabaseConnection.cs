using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Connections;

/// <summary>
/// Creates SQL Server connections using the configured connection string.
/// </summary>
public class DatabaseConnection : DatabaseConnectionStringProvider, IDatabaseConnection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseConnection"/> class.
    /// </summary>
    /// <param name="configuration">The configuration containing the connection string.</param>
    public DatabaseConnection(IConfiguration configuration)
        : base(configuration)
    {
    }

    /// <summary>
    /// Creates a new SQL Server connection using the configured connection string.
    /// </summary>
    /// <returns>A new, unopened <see cref="IDbConnection"/>.</returns>
    public IDbConnection GetConnection() => new SqlConnection(GetConnectionString());
}
