using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.RoundRobin;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Everything the writer needs for one league's season. The seed of the fixtures is the season's id.
/// </summary>
/// <param name="League">The league.</param>
/// <param name="Season">The newest season of the league.</param>
/// <param name="Divisions">The divisions of the season, in ascending order of their competitions' Order.</param>
/// <param name="Fixtures">
/// The fixtures of each division, in the order of <paramref name="Divisions"/>, each ordered by round.
/// </param>
/// <param name="Matchdays">The kickoff of each league round, in UTC; round r kicks off at index r - 1.</param>
internal sealed record LeagueInput(
    GetLeaguesResult League,
    GetSeasonsResult Season,
    IReadOnlyList<LeagueDivision> Divisions,
    IReadOnlyList<IReadOnlyList<RoundRobinMatch>> Fixtures,
    IReadOnlyList<DateTime> Matchdays);
