using Microsoft.Extensions.DependencyInjection;
using Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

namespace Softela.SoccerManager.Generator.Application;

/// <summary>
/// Registers the application layer's services with the dependency injection container.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Adds the fixture generator's services. They are scoped, so each function run gets its own instances, and they
    /// share that run's unit of work and repositories. The steps of a league's season run in the order they are
    /// registered here.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services
            .AddScoped<ILeagueFixtureService, LeagueFixtureService>()
            .AddScoped<LeagueFixtureWriter>()
            .AddScoped<MatchInserter>()
            .AddScoped<ILeagueStep, LeagueMatchesStep>()
            .AddScoped<ILeagueStep, DomesticCupStep>();

        return services;
    }
}
