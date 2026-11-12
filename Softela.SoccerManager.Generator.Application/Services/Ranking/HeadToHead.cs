using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// Puts participants that are level on points, goal difference and goals for in their final order: by the matches
/// among them while that separates them, and by a seeded shuffle for those it cannot.
/// </summary>
internal static class HeadToHead
{
    /// <summary>
    /// The name of the purpose a ranking shuffle derives its random source for.
    /// </summary>
    private const string RankingPurpose = "ranking";

    /// <summary>
    /// Orders a group of two or more participants that are level. Head-to-head is used only when every two members
    /// met the same number of times, at least once; otherwise the whole group is shuffled. Head-to-head ranks the
    /// group on the matches among its members only, and ranks any smaller group that is still level again on its own
    /// matches. When it separates nobody, that group is shuffled.
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

        List<IReadOnlyList<int>> segments = new();

        // The check is made once. A sub-group of a group that passed it has equal counts too.
        if (!HaveMetEqually(group, matches))
        {
            segments.Add(Shuffle(seasonId, group));

            return segments;
        }

        RankGroup(seasonId, group, matches, segments);

        return segments;
    }

    /// <summary>
    /// Ranks a group on the matches among its own members and adds its segments, in order, to the list.
    /// </summary>
    /// <param name="seasonId">The id of the season the table belongs to.</param>
    /// <param name="group">The ids of the participants that are level.</param>
    /// <param name="matches">The matches of the table.</param>
    /// <param name="segments">The segments so far; this method adds to it.</param>
    private static void RankGroup(
        int seasonId,
        IReadOnlyList<int> group,
        IReadOnlyList<TableMatch> matches,
        List<IReadOnlyList<int>> segments)
    {
        IReadOnlyDictionary<int, TableTotals> totals = TableTally.Count(group, matches);
        IReadOnlyList<IReadOnlyList<int>> subGroups = TableTally.LevelGroups(totals.Values);

        // One group is the whole group: head-to-head separates nobody.
        if (subGroups.Count == 1)
        {
            segments.Add(Shuffle(seasonId, group));

            return;
        }

        foreach (IReadOnlyList<int> subGroup in subGroups)
        {
            if (subGroup.Count == 1)
            {
                segments.Add(subGroup);

                continue;
            }

            RankGroup(seasonId, subGroup, matches, segments);
        }
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
