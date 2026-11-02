using System.Data;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Connections;

/// <summary>
/// Provides access to the application's database connection.
/// </summary>
public interface IDatabaseConnection
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <returns>A new, unopened <see cref="IDbConnection"/>.</returns>
    IDbConnection GetConnection();

    /// <summary>
    /// Retrieves the connection string used to connect to the database.
    /// </summary>
    /// <returns>The configured database connection string.</returns>
    string GetConnectionString();
}
