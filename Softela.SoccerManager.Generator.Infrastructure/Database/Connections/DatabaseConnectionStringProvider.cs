using Microsoft.Extensions.Configuration;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Connections;

/// <summary>
/// Resolves the database connection string from application configuration.
/// </summary>
public class DatabaseConnectionStringProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseConnectionStringProvider"/> class.
    /// </summary>
    /// <param name="configuration">The configuration containing the connection string.</param>
    public DatabaseConnectionStringProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Retrieves the configured "soccerteambuilder" connection string.
    /// </summary>
    /// <returns>The connection string used to connect to the database.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the "soccerteambuilder" connection string is missing or blank.</exception>
    public string GetConnectionString()
    {
        var connectionString = _configuration.GetConnectionString("soccerteambuilder");

        // An empty value reads back as "" rather than null, and SqlConnection only rejects that once it is opened,
        // so a blank value is refused here the same as a missing one.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'soccerteambuilder' not found.");
        }

        return connectionString;
    }
}
