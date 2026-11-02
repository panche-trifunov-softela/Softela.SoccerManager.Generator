using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database;
using Softela.SoccerManager.Generator.Infrastructure.Database.Connections;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure;

/// <summary>
/// Registers the infrastructure layer's service implementations.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Adds the infrastructure layer's service implementations to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration the infrastructure layer reads its settings from.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the 'soccerteambuilder' connection string is missing or blank.</exception>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved here rather than lazily so a missing connection string fails at startup, not at the first function run.
        _ = new DatabaseConnectionStringProvider(configuration).GetConnectionString();

        // SQL Server DATETIME2 carries no zone, so without this every timestamp read back is DateTimeKind.Unspecified.
        SqlMapper.AddTypeHandler(new UtcDateTimeHandler());
        // Maps DATE columns to DateOnly explicitly, so the round-trip does not depend on the driver's own DateOnly support.
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services
            .AddScoped<IDatabaseConnection, DatabaseConnection>()
            .AddScoped<IDapperDataContext, DapperDataContext>()
            .AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
