using Softela.SoccerManager.Generator.Application.Services.Ranking;

namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// Turns written matches into the input of the table ranking.
/// </summary>
public static class SeasonTableMatches
{
    /// <summary>
    /// Converts matches to table matches, each with its clubs and its goals, in the order given.
    /// </summary>
    /// <param name="matches">The matches, which the caller has checked to have both clubs.</param>
    /// <returns>One table match for each match, in the input order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matches"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a match lacks a club, which the caller was to have checked.
    /// </exception>
    public static List<TableMatch> Of(IEnumerable<SeasonMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        return matches
            .Select(match => new TableMatch(
                match.HomeLeagueTeamId!.Value,
                match.AwayLeagueTeamId!.Value,
                match.HomeGoals,
                match.AwayGoals))
            .ToList();
    }
}
