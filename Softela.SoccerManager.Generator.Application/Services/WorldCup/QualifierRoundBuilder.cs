using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Builds the matches of the World Cup qualifier groups: every group plays its own round robin, and the groups of one
/// number of legs are scheduled together. The result is fixed by the season. It reads nothing and writes nothing.
/// </summary>
internal static class QualifierRoundBuilder
{
    /// <summary>
    /// The start of the purpose the schedule's random source is derived with; the number of legs follows it.
    /// </summary>
    private const string RandomPurposePrefix = "worldCupQualifierRounds:";

    /// <summary>
    /// Builds the matches of the groups. Each group's rounds become matchdays: a group of 4 or 3 over 2 legs has
    /// matchdays 1 to 6 (a group of 3 has a bye in each round), a pair has 1 and 2, and a group of 5 over 1 leg has 1
    /// to 5.
    /// </summary>
    /// <param name="groups">The groups, each with its nations and its number of legs.</param>
    /// <param name="seasonId">The id of the season, which seeds the schedule.</param>
    /// <returns>
    /// Each match with the number of its group, sorted by matchday and then by group number, as the date planner needs.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groups"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the scheduler cannot schedule a group.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a group has a number of legs other than 1 or 2.
    /// </exception>
    public static IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> Build(
        IReadOnlyList<QualifierGroup> groups,
        int seasonId)
    {
        ArgumentNullException.ThrowIfNull(groups);

        List<WorldCupGroup> worldCupGroups = groups
            .Select(group => new WorldCupGroup(group.GroupNumber, group.LeagueTeamIds, group.Legs))
            .ToList();

        return WorldCupRoundBuilder.Build(worldCupGroups, seasonId, RandomPurposePrefix);
    }
}
