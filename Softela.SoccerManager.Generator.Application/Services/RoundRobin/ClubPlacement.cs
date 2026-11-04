using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// Decides which club takes which position of its group's table.
/// </summary>
internal static class ClubPlacement
{
    /// <summary>
    /// Places the clubs of the shared-stadium pairs first, then shuffles the rest into the free positions. The order
    /// of the random draws is fixed, so the same input and random state always give the same placement.
    /// </summary>
    /// <param name="sortedGroups">The clubs of each group, in ascending id order.</param>
    /// <param name="sortedPairs">The pairs, each with the smaller id first, sorted by first and then second id.</param>
    /// <param name="groupOfClub">The index of the group each club belongs to.</param>
    /// <param name="complementaryBySize">The never-both-home position pairs, by group size.</param>
    /// <param name="random">The random source; its state advances.</param>
    /// <returns>For each group, the club id at each position.</returns>
    public static IReadOnlyList<IReadOnlyList<int>> Assign(
        IReadOnlyList<IReadOnlyList<int>> sortedGroups,
        IReadOnlyList<SharedStadiumPair> sortedPairs,
        IReadOnlyDictionary<int, int> groupOfClub,
        IReadOnlyDictionary<int, IReadOnlyList<(int First, int Second)>> complementaryBySize,
        SeededRandom random)
    {
        int[][] clubAt = sortedGroups.Select(group => new int[group.Count]).ToArray();
        bool[][] taken = sortedGroups.Select(group => new bool[group.Count]).ToArray();
        HashSet<int> placedClubs = new();

        foreach (SharedStadiumPair pair in sortedPairs)
        {
            int firstGroup = groupOfClub[pair.FirstClubId];
            int secondGroup = groupOfClub[pair.SecondClubId];

            List<(int FirstPosition, int SecondPosition)> options = FindOptions(
                complementaryBySize[sortedGroups[firstGroup].Count],
                taken[firstGroup],
                taken[secondGroup]);

            // Never empty: a taken position only removes the options that start or end on it, and the validator's
            // limit of half a group in pairs keeps too few positions taken to remove them all.
            (int firstPosition, int secondPosition) = options[random.NextInt(options.Count)];

            clubAt[firstGroup][firstPosition] = pair.FirstClubId;
            taken[firstGroup][firstPosition] = true;

            clubAt[secondGroup][secondPosition] = pair.SecondClubId;
            taken[secondGroup][secondPosition] = true;

            placedClubs.Add(pair.FirstClubId);
            placedClubs.Add(pair.SecondClubId);
        }

        for (int group = 0; group < sortedGroups.Count; group++)
        {
            FillFreePositions(sortedGroups[group], placedClubs, clubAt[group], taken[group], random);
        }

        return clubAt;
    }

    /// <summary>
    /// Lists the ways one pair can take free positions, in the order the complementary list gives them.
    /// </summary>
    /// <param name="complementary">The never-both-home position pairs for the group size.</param>
    /// <param name="firstTaken">Which positions of the first club's group are already taken.</param>
    /// <param name="secondTaken">Which positions of the second club's group are already taken.</param>
    /// <returns>
    /// The (first club position, second club position) options; for each complementary pair, the straight option
    /// comes before the swapped one.
    /// </returns>
    private static List<(int FirstPosition, int SecondPosition)> FindOptions(
        IReadOnlyList<(int First, int Second)> complementary,
        bool[] firstTaken,
        bool[] secondTaken)
    {
        List<(int FirstPosition, int SecondPosition)> options = new();

        foreach ((int low, int high) in complementary)
        {
            if (!firstTaken[low] && !secondTaken[high])
            {
                options.Add((low, high));
            }

            if (!firstTaken[high] && !secondTaken[low])
            {
                options.Add((high, low));
            }
        }

        return options;
    }

    /// <summary>
    /// Shuffles the clubs that have no position yet and gives them the free positions in ascending order.
    /// </summary>
    /// <param name="sortedClubs">The clubs of the group, in ascending id order.</param>
    /// <param name="placedClubs">The clubs that already have a position.</param>
    /// <param name="clubAt">The club at each position of the group; free positions are filled in.</param>
    /// <param name="taken">Which positions of the group are taken.</param>
    /// <param name="random">The random source; its state advances.</param>
    private static void FillFreePositions(
        IReadOnlyList<int> sortedClubs,
        HashSet<int> placedClubs,
        int[] clubAt,
        bool[] taken,
        SeededRandom random)
    {
        List<int> rest = sortedClubs.Where(clubId => !placedClubs.Contains(clubId)).ToList();

        random.Shuffle(rest);

        int next = 0;

        for (int position = 0; position < taken.Length; position++)
        {
            if (!taken[position])
            {
                clubAt[position] = rest[next];
                next++;
            }
        }
    }
}
