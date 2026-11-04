using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// Generates round robin fixtures for one or more groups of clubs.
/// </summary>
public static class RoundRobinScheduler
{
    /// <summary>
    /// Generates the matches of every group. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Each pair of clubs in a group meets once per leg; over 2 legs, once at each home.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Over 2 legs, the second half plays the first half's rounds in the same order, with the venues reversed.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// No club plays more than 2 home or 2 away in a row, and a bye does not count. The one exception is a group of
    /// exactly 4 clubs over 2 legs, where 3 in a row at the halfway point cannot be avoided.
    /// </description>
    /// </item>
    /// <item><description>Over 2 legs a club has as many home matches as away.</description></item>
    /// <item>
    /// <description>
    /// The two clubs of a shared-stadium pair are never both at home in the same round number, also when they are in
    /// different groups of equal size, because those groups use the same table.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The result depends only on the random source's state, the order of the groups, and the sets of clubs and
    /// pairs, so the order in which rows came back from a database cannot change it.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="groups">The club ids of each group; every group plays its own round robin.</param>
    /// <param name="legs">The number of legs, 1 or 2.</param>
    /// <param name="sharedStadiums">
    /// The pairs of clubs that share a stadium; a pair may be inside one group or across two groups of equal size.
    /// </param>
    /// <param name="random">The random source; its state advances.</param>
    /// <returns>
    /// One list of matches per input group, in input order, ordered by round; the caller's lists are not changed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="groups"/>, <paramref name="sharedStadiums"/> or <paramref name="random"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the groups or pairs cannot be scheduled, for example a group of fewer than 2 clubs, a club twice,
    /// or more than half of a group's clubs in pairs.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="legs"/> is not 1 or 2.</exception>
    public static IReadOnlyList<IReadOnlyList<RoundRobinMatch>> Generate(
        IReadOnlyList<IReadOnlyList<int>> groups,
        int legs,
        IReadOnlyList<SharedStadiumPair> sharedStadiums,
        SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(sharedStadiums);
        ArgumentNullException.ThrowIfNull(random);

        IReadOnlyDictionary<int, int> groupOfClub = RoundRobinInputValidator.Validate(groups, sharedStadiums);

        // Sorted copies, so the order the caller's rows arrived in cannot change the result and the caller's lists stay
        // untouched.
        List<IReadOnlyList<int>> sortedGroups = groups
            .Select(group => (IReadOnlyList<int>)group.OrderBy(clubId => clubId).ToList())
            .ToList();

        List<SharedStadiumPair> sortedPairs = sharedStadiums
            .Select(pair => new SharedStadiumPair(
                Math.Min(pair.FirstClubId, pair.SecondClubId),
                Math.Max(pair.FirstClubId, pair.SecondClubId)))
            .OrderBy(pair => pair.FirstClubId)
            .ThenBy(pair => pair.SecondClubId)
            .ToList();

        Dictionary<int, IReadOnlyList<IReadOnlyList<PositionMatch>>> tableBySize = new();
        Dictionary<int, IReadOnlyList<(int First, int Second)>> complementaryBySize = new();

        foreach (int size in sortedGroups.Select(group => group.Count).Distinct())
        {
            IReadOnlyList<IReadOnlyList<PositionMatch>> table = BergerTable.Build(size, legs);

            tableBySize[size] = table;
            complementaryBySize[size] = ComplementaryPositions.Find(table, size);
        }

        IReadOnlyList<IReadOnlyList<int>> clubAt = ClubPlacement.Assign(
            sortedGroups,
            sortedPairs,
            groupOfClub,
            complementaryBySize,
            random);

        return sortedGroups
            .Select((group, index) => ToMatches(tableBySize[group.Count], clubAt[index]))
            .ToList();
    }

    /// <summary>
    /// Turns a table in positions into matches between the clubs placed on it.
    /// </summary>
    /// <param name="table">The table to read.</param>
    /// <param name="clubAt">The club id at each position.</param>
    /// <returns>The matches, round by round, with 1-based round numbers.</returns>
    private static IReadOnlyList<RoundRobinMatch> ToMatches(
        IReadOnlyList<IReadOnlyList<PositionMatch>> table,
        IReadOnlyList<int> clubAt)
    {
        List<RoundRobinMatch> matches = new();

        for (int roundIndex = 0; roundIndex < table.Count; roundIndex++)
        {
            foreach (PositionMatch entry in table[roundIndex])
            {
                matches.Add(new RoundRobinMatch(roundIndex + 1, clubAt[entry.Home], clubAt[entry.Away]));
            }
        }

        return matches;
    }
}
