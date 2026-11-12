namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// Ranks a league table from finished matches, for a division, a cup's league phase and later the groups of a World
/// Cup. The order is points (3 for a win, 1 for a draw, and never a shootout), then goal difference, then goals for.
/// Participants that are still level are ranked head-to-head, on the matches among themselves only, when every two of
/// them met the same number of times, at least once. A smaller group that is still level is ranked head-to-head again
/// on its own matches. What head-to-head cannot separate is shuffled, seeded by the id of the season and the ids of the
/// participants still level, so every caller that ranks the same table gets the same order.
/// </summary>
public static class TableRanking
{
    /// <summary>
    /// Ranks a table. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>The input lists are not changed, and a new list is returned.</description>
    /// </item>
    /// <item>
    /// <description>The order of either input list never changes the result.</description>
    /// </item>
    /// <item>
    /// <description>Every participant gets one row, also a participant with no match.</description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="seasonId">
    /// The id of the season the table belongs to; in the rollover it is the finished season. Every caller that ranks
    /// the same table passes the same id.
    /// </param>
    /// <param name="participants">The ids of every participant of the table.</param>
    /// <param name="matches">
    /// The finished matches of the table, and no others. An unplayed match reads 0-0 and would count as a draw, and
    /// the ranking cannot tell such a match, a duplicate or another table's match from a real result, so the caller
    /// picks the matches.
    /// </param>
    /// <returns>One row for each participant, from the first place to the last.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="participants"/> or <paramref name="matches"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a participant is listed more than once, or a match is missing from the list, has a side that is
    /// not a participant, has the same participant on both sides, or has negative goals.
    /// </exception>
    public static IReadOnlyList<TableRow> Rank(
        int seasonId,
        IReadOnlyList<int> participants,
        IReadOnlyList<TableMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(matches);

        TableRankingInputValidator.Validate(participants, matches);

        IReadOnlyDictionary<int, TableTotals> totals = TableTally.Count(participants, matches);
        List<IReadOnlyList<int>> segments = new();

        foreach (IReadOnlyList<int> group in TableTally.LevelGroups(totals.Values))
        {
            if (group.Count == 1)
            {
                segments.Add(group);

                continue;
            }

            segments.AddRange(HeadToHead.Order(seasonId, group, matches));
        }

        return ToRows(segments, totals);
    }

    /// <summary>
    /// Turns the ordered segments into rows, with places from 1.
    /// </summary>
    /// <param name="segments">The segments in final order; a segment of two or more ids was shuffled.</param>
    /// <param name="totals">The overall totals of every participant.</param>
    /// <returns>The rows in the order of the segments.</returns>
    private static List<TableRow> ToRows(
        List<IReadOnlyList<int>> segments,
        IReadOnlyDictionary<int, TableTotals> totals)
    {
        List<TableRow> rows = new(totals.Count);

        foreach (IReadOnlyList<int> segment in segments)
        {
            int firstPlace = rows.Count + 1;
            int? shuffledFromPlace = segment.Count >= 2 ? firstPlace : null;

            foreach (int id in segment)
            {
                TableTotals total = totals[id];

                rows.Add(new TableRow(
                    rows.Count + 1,
                    id,
                    total.Played,
                    total.Wins,
                    total.Draws,
                    total.Losses,
                    total.GoalsFor,
                    total.GoalsAgainst,
                    total.Points,
                    shuffledFromPlace));
            }
        }

        return rows;
    }
}
