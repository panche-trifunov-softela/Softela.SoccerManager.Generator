using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.RoundRobin;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Builds the matches of World Cup round-robin groups, in the qualifiers and in the finals: every group plays its own
/// round robin, and the groups of one number of legs are scheduled together. The result is fixed by the season and the
/// purpose prefix. It reads nothing and writes nothing.
/// </summary>
internal static class WorldCupRoundBuilder
{
    /// <summary>
    /// Builds the matches of the groups. Each group's rounds become matchdays: a group of 4 or 3 over 2 legs has
    /// matchdays 1 to 6 (a group of 3 has a bye in each round), a pair has 1 and 2, a group of 5 over 1 leg has 1 to 5,
    /// and a group of 4 over 1 leg has 1 to 3.
    /// </summary>
    /// <param name="groups">The groups, each with its nations and its number of legs.</param>
    /// <param name="seasonId">The id of the season, which seeds the schedule.</param>
    /// <param name="purposePrefix">
    /// The start of the purpose the schedule's random source is derived with; the number of legs follows it.
    /// </param>
    /// <returns>
    /// Each match with the number of its group, sorted by matchday and then by group number, as the date planner needs.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="groups"/> or <paramref name="purposePrefix"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the scheduler cannot schedule a group.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a group has a number of legs other than 1 or 2.
    /// </exception>
    internal static IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> Build(
        IReadOnlyList<WorldCupGroup> groups,
        int seasonId,
        string purposePrefix)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(purposePrefix);

        SeededRandom seeded = new(seasonId);
        List<(byte GroupNumber, ContinentalFixture Fixture)> fixtures = [];

        foreach (int legs in groups.Select(group => group.Legs).Distinct().OrderByDescending(count => count))
        {
            List<WorldCupGroup> ofLegs = groups.Where(group => group.Legs == legs).ToList();

            SeededRandom random = seeded.Derive(purposePrefix + legs.ToString(CultureInfo.InvariantCulture));

            IReadOnlyList<IReadOnlyList<RoundRobinMatch>> schedules = RoundRobinScheduler.Generate(
                ofLegs.Select(group => group.LeagueTeamIds).ToList(),
                legs,
                [],
                random);

            for (int index = 0; index < ofLegs.Count; index++)
            {
                foreach (RoundRobinMatch match in schedules[index])
                {
                    fixtures.Add((
                        ofLegs[index].GroupNumber,
                        new ContinentalFixture(match.Round, match.HomeClubId, match.AwayClubId)));
                }
            }
        }

        // OrderBy is stable, so the matches of a group keep the scheduler's order within a matchday.
        return fixtures
            .OrderBy(entry => entry.Fixture.Matchday)
            .ThenBy(entry => entry.GroupNumber)
            .ToList();
    }
}
