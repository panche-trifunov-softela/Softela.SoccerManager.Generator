namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// Finds the pairs of table positions that are never at home in the same round.
/// </summary>
internal static class ComplementaryPositions
{
    /// <summary>
    /// Lists every pair of positions that has no round in which both are at home. A bye counts as not at home.
    /// </summary>
    /// <param name="rounds">The table to inspect.</param>
    /// <param name="clubCount">The number of positions that hold a club; the bye position is not one of them.</param>
    /// <returns>The pairs, with First below Second, in ascending order of First and then Second.</returns>
    public static IReadOnlyList<(int First, int Second)> Find(
        IReadOnlyList<IReadOnlyList<PositionMatch>> rounds,
        int clubCount)
    {
        bool[,] bothHome = new bool[clubCount, clubCount];

        foreach (IReadOnlyList<PositionMatch> round in rounds)
        {
            // Marks every pair of home positions in the round (the diagonal too, which is never read).
            foreach (PositionMatch match in round)
            {
                foreach (PositionMatch other in round)
                {
                    bothHome[match.Home, other.Home] = true;
                }
            }
        }

        List<(int First, int Second)> pairs = new();

        for (int first = 0; first < clubCount; first++)
        {
            for (int second = first + 1; second < clubCount; second++)
            {
                if (!bothHome[first, second])
                {
                    pairs.Add((first, second));
                }
            }
        }

        return pairs;
    }
}
