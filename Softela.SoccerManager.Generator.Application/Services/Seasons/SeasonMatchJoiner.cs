using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// Joins the rows the database gives for a season, its matches and its scores, into the matches that the generator's
/// steps read.
/// </summary>
internal static class SeasonMatchJoiner
{
    /// <summary>
    /// Joins each match of the season with the score rows of its two clubs. A side is set only when the match has
    /// exactly one score row for it; otherwise the club is null, its goals are 0 and its shootout goals are null. The
    /// goals of an unknown side are 0 because the record holds an int, and the reader never decides on a side it does
    /// not know. This method refuses nothing: a match with a missing side is the reader's to refuse.
    /// </summary>
    /// <param name="matches">The season's matches; the result keeps their order.</param>
    /// <param name="scores">
    /// The season's score rows, one for each club of each match that has statistic rows. A row for a match that is not
    /// in <paramref name="matches"/> is ignored.
    /// </param>
    /// <returns>One season match for each match given.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="matches"/> or <paramref name="scores"/> is null.
    /// </exception>
    internal static IReadOnlyList<SeasonMatch> Join(
        IReadOnlyList<GetMatchesBySeasonResult> matches,
        IReadOnlyList<GetMatchScoresBySeasonResult> scores)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(scores);

        ILookup<int, GetMatchScoresBySeasonResult> scoresByMatch = scores.ToLookup(score => score.MatchId);
        List<SeasonMatch> joined = new(matches.Count);

        foreach (GetMatchesBySeasonResult match in matches)
        {
            GetMatchScoresBySeasonResult[] rows = scoresByMatch[match.Id].ToArray();
            GetMatchScoresBySeasonResult? home = SingleSide(rows, isHomeTeam: true);
            GetMatchScoresBySeasonResult? away = SingleSide(rows, isHomeTeam: false);

            joined.Add(new SeasonMatch(
                match.Id,
                match.CompetitionId,
                match.StartDateTime,
                match.IsFinished,
                match.Stage,
                match.RoundNumber,
                match.Leg,
                home?.LeagueTeamId,
                away?.LeagueTeamId,
                home?.Goals ?? 0,
                away?.Goals ?? 0,
                home?.ShootoutGoals,
                away?.ShootoutGoals));
        }

        return joined;
    }

    /// <summary>
    /// Picks the score row of one side of a match, provided the match has exactly one row for that side.
    /// </summary>
    /// <param name="rows">The score rows of one match.</param>
    /// <param name="isHomeTeam">Whether to pick the home side or the away side.</param>
    /// <returns>The only row of the side, or null when the match has none or several.</returns>
    private static GetMatchScoresBySeasonResult? SingleSide(
        IReadOnlyList<GetMatchScoresBySeasonResult> rows,
        bool isHomeTeam)
    {
        GetMatchScoresBySeasonResult[] side = rows.Where(row => row.IsHomeTeam == isHomeTeam).Take(2).ToArray();

        return side.Length == 1 ? side[0] : null;
    }
}
