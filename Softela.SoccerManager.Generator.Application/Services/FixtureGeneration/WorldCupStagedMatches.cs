using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Turns the matches of the World Cup groups into the matches to insert, for the qualifier step and the finals group
/// step, which differ only in the stage they write.
/// </summary>
internal static class WorldCupStagedMatches
{
    /// <summary>
    /// Turns the matches of the groups into the matches to insert, in the order given.
    /// </summary>
    /// <param name="national">The league's national competition.</param>
    /// <param name="fixtures">The matches with the number of their group.</param>
    /// <param name="kickoffs">The kickoff of each match, in the order of <paramref name="fixtures"/>.</param>
    /// <param name="stage">The stage of every match: a qualifier group or a finals group.</param>
    /// <returns>One staged match for each fixture.</returns>
    /// <exception cref="OverflowException">
    /// Thrown when a matchday does not fit in a byte, which no group reaches.
    /// </exception>
    internal static List<StagedMatch> Of(
        GetCompetitionsResult national,
        IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> fixtures,
        IReadOnlyList<DateTime> kickoffs,
        MatchStage stage)
    {
        List<StagedMatch> matches = [];

        for (int index = 0; index < fixtures.Count; index++)
        {
            ContinentalFixture fixture = fixtures[index].Fixture;

            matches.Add(new StagedMatch(
                national.Id,
                kickoffs[index],
                stage,
                checked((byte)fixture.Matchday),
                Leg: null,
                fixture.HomeLeagueTeamId,
                fixture.AwayLeagueTeamId,
                fixtures[index].GroupNumber));
        }

        return matches;
    }
}
