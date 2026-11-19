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
    /// registered here: the league matches, the domestic cup, the continental cups' league phase, then their
    /// knockouts, then the World Cup qualifiers, the World Cup finals groups and the World Cup knockouts, and then the
    /// promotion playoffs.
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
            .AddScoped<ILeagueStep, DomesticCupStep>()
            .AddScoped<ILeagueStep, ContinentalCupStep>()
            .AddScoped<ILeagueStep, ContinentalKnockoutStep>()
            .AddScoped<ILeagueStep, WorldCupQualifierStep>()
            .AddScoped<ILeagueStep, WorldCupGroupStep>()
            .AddScoped<ILeagueStep, WorldCupKnockoutStep>()
            .AddScoped<ILeagueStep, PlayoffStep>();

        return services;
    }
}
