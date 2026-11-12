namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// Counts the matches of a table and cuts the participants into groups that are level. The table and head-to-head both
/// use it, so the order of points, goal difference and goals for is written in one place only.
/// </summary>
internal static class TableTally
{
    /// <summary>
    /// The points a win gives.
    /// </summary>
    private const int PointsForWin = 3;

    /// <summary>
    /// The points a draw gives to each side.
    /// </summary>
    private const int PointsForDraw = 1;

    /// <summary>
    /// Counts the totals of each participant over the matches among the given participants.
    /// </summary>
    /// <param name="ids">The ids of the participants to count; each one gets an entry.</param>
    /// <param name="matches">
    /// The matches to count from. A match counts only when both its sides are in <paramref name="ids"/>; the others are
    /// ignored.
    /// </param>
    /// <returns>The totals by participant id; a participant with no counted match has all zeros.</returns>
    internal static IReadOnlyDictionary<int, TableTotals> Count(
        IReadOnlyCollection<int> ids,
        IReadOnlyList<TableMatch> matches)
    {
        Dictionary<int, TableTotals> totals = new();

        foreach (int id in ids)
        {
            totals[id] = new TableTotals(id, 0, 0, 0, 0, 0, 0, 0);
        }

        foreach (TableMatch match in matches)
        {
            if (!totals.ContainsKey(match.HomeId) || !totals.ContainsKey(match.AwayId))
            {
                continue;
            }

            totals[match.HomeId] = AddMatch(totals[match.HomeId], match.HomeGoals, match.AwayGoals);
            totals[match.AwayId] = AddMatch(totals[match.AwayId], match.AwayGoals, match.HomeGoals);
        }

        return totals;
    }

    /// <summary>
    /// Cuts the participants into groups that are level, from the best group to the worst. The order is more points,
    /// then a better goal difference, then more goals for, then a lower id. Two participants are level when the first
    /// three keys are equal; the id only keeps the order of a group fixed.
    /// </summary>
    /// <param name="totals">The totals of the participants to order.</param>
    /// <returns>The groups of ids, best group first, each group in ascending id.</returns>
    internal static IReadOnlyList<IReadOnlyList<int>> LevelGroups(IEnumerable<TableTotals> totals)
    {
        return totals
            .OrderByDescending(KeyOf)
            .ThenBy(total => total.Id)
            .GroupBy(KeyOf)
            .Select(group => (IReadOnlyList<int>)group.Select(total => total.Id).ToList())
            .ToList();
    }

    /// <summary>
    /// Adds one match to a participant's totals.
    /// </summary>
    /// <param name="totals">The totals so far.</param>
    /// <param name="goalsFor">The goals the participant scored in the match.</param>
    /// <param name="goalsAgainst">The goals the participant conceded in the match.</param>
    /// <returns>The new totals; the given ones are not changed.</returns>
    private static TableTotals AddMatch(TableTotals totals, int goalsFor, int goalsAgainst)
    {
        int wins = totals.Wins;
        int draws = totals.Draws;
        int losses = totals.Losses;
        int points = totals.Points;

        if (goalsFor > goalsAgainst)
        {
            wins++;
            points += PointsForWin;
        }
        else if (goalsFor == goalsAgainst)
        {
            draws++;
            points += PointsForDraw;
        }
        else
        {
            losses++;
        }

        return totals with
        {
            Played = totals.Played + 1,
            Wins = wins,
            Draws = draws,
            Losses = losses,
            GoalsFor = totals.GoalsFor + goalsFor,
            GoalsAgainst = totals.GoalsAgainst + goalsAgainst,
            Points = points,
        };
    }

    /// <summary>
    /// The three keys a table is ordered and cut by: points, goal difference and goals for. A higher key is better, and
    /// two participants with the same key are level.
    /// </summary>
    /// <param name="totals">A participant's totals.</param>
    /// <returns>The points, the goal difference and the goals for.</returns>
    private static (int Points, int GoalDifference, int GoalsFor) KeyOf(TableTotals totals)
    {
        return (totals.Points, totals.GoalDifference, totals.GoalsFor);
    }
}
