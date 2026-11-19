using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// Puts participants that are level in their final order: by the matches among them while that separates them, and by a
/// seeded shuffle for those it cannot. <see cref="Order"/> does so for the club order, where the participants are level
/// on points, goal difference and goals for. <see cref="OrderWorldCup"/> does so for the World Cup order, where they
/// are level on points only and what the matches among them cannot separate goes to the overall goals first.
/// </summary>
internal static class HeadToHead
{
    /// <summary>
    /// The name of the purpose a ranking shuffle derives its random source for.
    /// </summary>
    private const string RankingPurpose = "ranking";

    /// <summary>
    /// Orders a group of two or more participants that are level in the club order. Head-to-head is used only when
    /// every two members met the same number of times, at least once; otherwise the whole group is shuffled.
    /// Head-to-head ranks the group on the matches among its members only, and ranks any smaller group that is still
    /// level again on its own matches. When it separates nobody, that group is shuffled.
    /// </summary>
    /// <param name="seasonId">The id of the season the table belongs to; it seeds the shuffle.</param>
    /// <param name="group">The ids of the participants that are level.</param>
    /// <param name="matches">The matches of the table; the ones outside the group are ignored.</param>
    /// <returns>
    /// The group as consecutive segments in final order. A segment of one id was placed by head-to-head. A segment of
    /// two or more ids was put in order by the seeded shuffle, in that order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="group"/> or <paramref name="matches"/> is null.
    /// </exception>
    internal static IReadOnlyList<IReadOnlyList<int>> Order(
        int seasonId,
        IReadOnlyList<int> group,
        IReadOnlyList<TableMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(matches);

        return Separate(group, matches, level => [Shuffle(seasonId, level)]);
    }

    /// <summary>
    /// Orders a group of two or more participants that are level on points, in the World Cup order. Head-to-head is
    /// used as <see cref="Order"/> uses it, only when every two members met the same number of times, at least once,
    /// and its mini-table is cut by points, goal difference and goals for. What head-to-head cannot separate, and the
    /// whole group when it was not used, is put in order by the overall goal difference, then the overall goals for,
    /// and the members still level after that are shuffled. Head-to-head is not applied again after the overall goal
    /// difference has split a group.
    /// </summary>
    /// <param name="seasonId">The id of the season the table belongs to; it seeds the shuffle.</param>
    /// <param name="group">The ids of the participants that are level on points.</param>
    /// <param name="matches">The matches of the table; the ones outside the group are ignored.</param>
    /// <param name="overallTotals">The totals of every participant over all the matches of the table.</param>
    /// <returns>
    /// The group as consecutive segments in final order. A segment of one id was placed by head-to-head or by the
    /// overall goals. A segment of two or more ids was put in order by the seeded shuffle, in that order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="group"/>, <paramref name="matches"/> or <paramref name="overallTotals"/> is null.
    /// </exception>
    internal static IReadOnlyList<IReadOnlyList<int>> OrderWorldCup(
        int seasonId,
        IReadOnlyList<int> group,
        IReadOnlyList<TableMatch> matches,
        IReadOnlyDictionary<int, TableTotals> overallTotals)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(overallTotals);

        return Separate(group, matches, level => ByOverallGoals(seasonId, level, overallTotals));
    }

    /// <summary>
    /// Puts a group in order by head-to-head where it can be used, and gives every part it cannot separate to a
    /// caller-chosen last step.
    /// </summary>
    /// <param name="group">The ids of the participants that are level.</param>
    /// <param name="matches">The matches of the table.</param>
    /// <param name="unsplit">
    /// Puts a group that head-to-head did not separate in order, as segments: the whole group when head-to-head could
    /// not be used, or a smaller group that its own matches left level.
    /// </param>
    /// <returns>The group as consecutive segments in final order.</returns>
    private static List<IReadOnlyList<int>> Separate(
        IReadOnlyList<int> group,
        IReadOnlyList<TableMatch> matches,
        Func<IReadOnlyList<int>, IReadOnlyList<IReadOnlyList<int>>> unsplit)
    {
        List<IReadOnlyList<int>> segments = new();

        // The check is made once. A sub-group of a group that passed it has equal counts too.
        if (!HaveMetEqually(group, matches))
        {
            segments.AddRange(unsplit(group));

            return segments;
        }

        RankGroup(group, matches, segments, unsplit);

        return segments;
    }

    /// <summary>
    /// Ranks a group on the matches among its own members and adds its segments, in order, to the list.
    /// </summary>
    /// <param name="group">The ids of the participants that are level.</param>
    /// <param name="matches">The matches of the table.</param>
    /// <param name="segments">The segments so far; this method adds to it.</param>
    /// <param name="unsplit">Puts a group that its own matches leave level in order, as segments.</param>
    private static void RankGroup(
        IReadOnlyList<int> group,
        IReadOnlyList<TableMatch> matches,
        List<IReadOnlyList<int>> segments,
        Func<IReadOnlyList<int>, IReadOnlyList<IReadOnlyList<int>>> unsplit)
    {
        IReadOnlyDictionary<int, TableTotals> totals = TableTally.Count(group, matches);
        IReadOnlyList<IReadOnlyList<int>> subGroups = TableTally.LevelGroups(totals.Values);

        // One group is the whole group: head-to-head separates nobody.
        if (subGroups.Count == 1)
        {
            segments.AddRange(unsplit(group));

            return;
        }

        foreach (IReadOnlyList<int> subGroup in subGroups)
        {
            if (subGroup.Count == 1)
            {
                segments.Add(subGroup);

                continue;
            }

            RankGroup(subGroup, matches, segments, unsplit);
        }
    }

    /// <summary>
    /// Puts a group in order by the overall goal difference, then the overall goals for, and shuffles the members that
    /// are still level after both.
    /// </summary>
    /// <param name="seasonId">The id of the season the table belongs to; it seeds the shuffle.</param>
    /// <param name="group">The ids of the participants to order.</param>
    /// <param name="overallTotals">The totals of every participant over all the matches of the table.</param>
    /// <returns>The group as consecutive segments in final order.</returns>
    private static List<IReadOnlyList<int>> ByOverallGoals(
        int seasonId,
        IReadOnlyList<int> group,
        IReadOnlyDictionary<int, TableTotals> overallTotals)
    {
        List<IReadOnlyList<int>> segments = new();

        foreach (IReadOnlyList<int> level in TableTally.GoalGroups(group.Select(id => overallTotals[id])))
        {
            segments.Add(level.Count == 1 ? level : Shuffle(seasonId, level));
        }

        return segments;
    }

    /// <summary>
    /// Tells whether every two members of the group met the same number of times, and at least once.
    /// </summary>
    /// <param name="group">The ids of the participants.</param>
    /// <param name="matches">The matches of the table; the ones outside the group are ignored.</param>
    /// <returns>True when the number of meetings is the same for every pair and is not 0.</returns>
    private static bool HaveMetEqually(IReadOnlyList<int> group, IReadOnlyList<TableMatch> matches)
    {
        HashSet<int> members = new(group);
        Dictionary<(int Low, int High), int> meetings = new();

        foreach (TableMatch match in matches)
        {
            if (!members.Contains(match.HomeId) || !members.Contains(match.AwayId))
            {
                continue;
            }

            (int Low, int High) pair = PairOf(match.HomeId, match.AwayId);

            meetings[pair] = meetings.GetValueOrDefault(pair) + 1;
        }

        int expected = 0;

        for (int first = 0; first < group.Count; first++)
        {
            for (int second = first + 1; second < group.Count; second++)
            {
                int count = meetings.GetValueOrDefault(PairOf(group[first], group[second]));

                if (count == 0 || (expected != 0 && count != expected))
                {
                    return false;
                }

                expected = count;
            }
        }

        return true;
    }

    /// <summary>
    /// Makes the key of a pair of participants, the same whichever of them is at home.
    /// </summary>
    /// <param name="first">One id.</param>
    /// <param name="second">The other id.</param>
    /// <returns>The lower id and the higher id.</returns>
    private static (int Low, int High) PairOf(int first, int second)
    {
        return (Math.Min(first, second), Math.Max(first, second));
    }

    /// <summary>
    /// Puts a group in order with a seeded shuffle. The source is derived from the season and from the ids of the
    /// group, so the order depends on nothing else: not on the order the ids came in, and not on another group's
    /// shuffle.
    /// </summary>
    /// <param name="seasonId">The id of the season the table belongs to.</param>
    /// <param name="group">The ids to shuffle.</param>
    /// <returns>A new list of the same ids in shuffled order; the input is not changed.</returns>
    private static List<int> Shuffle(int seasonId, IReadOnlyList<int> group)
    {
        List<int> sorted = group.OrderBy(id => id).ToList();
        string ids = string.Join(",", sorted.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        string purpose = RankingPurpose + ":" + ids;
        SeededRandom random = new SeededRandom(seasonId).Derive(purpose);

        random.Shuffle(sorted);

        return sorted;
    }
}
