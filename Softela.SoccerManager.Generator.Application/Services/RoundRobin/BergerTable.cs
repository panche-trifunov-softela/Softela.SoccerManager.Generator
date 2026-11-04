namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// Builds a Berger round robin table in positions, so any set of clubs of that size can be placed on it.
/// </summary>
public static class BergerTable
{
    private const int MinClubCount = 2;
    private const int OneLeg = 1;
    private const int TwoLegs = 2;

    // The classic table's first round gives some clubs two home or two away at the start of a leg, and a second leg
    // in the same order would turn that into three in a row at the halfway point. Its 4th round (index 3) is the only
    // start that keeps both ends of a leg clear of that at every size, except exactly 4 clubs, where it cannot be
    // avoided.
    private const int LegStartRoundIndex = 3;

    /// <summary>
    /// Builds the table for the given number of clubs. An odd count is padded with a bye position and the matches of
    /// that position are removed, so each round has one club with a bye. With 2 legs, the second leg plays the first
    /// leg's rounds in the same order with the venues reversed.
    /// </summary>
    /// <param name="clubCount">The number of clubs; it must be at least 2.</param>
    /// <param name="legs">The number of legs, 1 or 2.</param>
    /// <returns>
    /// The rounds in order, each holding its matches in positions; there are (table size - 1) * legs of them.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="clubCount"/> is below 2, or <paramref name="legs"/> is not 1 or 2.
    /// </exception>
    public static IReadOnlyList<IReadOnlyList<PositionMatch>> Build(int clubCount, int legs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(clubCount, MinClubCount);

        if (legs != OneLeg && legs != TwoLegs)
        {
            throw new ArgumentOutOfRangeException(nameof(legs), legs, "The number of legs must be 1 or 2.");
        }

        int tableSize = clubCount % 2 == 0 ? clubCount : clubCount + 1;

        List<List<PositionMatch>> firstLeg = BuildLeg(tableSize);

        if (clubCount != tableSize)
        {
            // The table's extra position is the bye; it is the last one, whose number equals clubCount.
            firstLeg = firstLeg.Select(round => RemoveMatchesOf(round, clubCount)).ToList();
        }

        List<List<PositionMatch>> rounds = new(firstLeg);

        if (legs == TwoLegs)
        {
            rounds.AddRange(firstLeg.Select(Flip));
        }

        return rounds;
    }

    /// <summary>
    /// Builds the rounds of one leg of an even-sized table, starting at the round that keeps the ends of a leg clear
    /// of runs and wrapping around to the rounds before it.
    /// </summary>
    /// <param name="tableSize">The even number of positions.</param>
    /// <returns>The rounds of the leg; the last position is the fixed one.</returns>
    private static List<List<PositionMatch>> BuildLeg(int tableSize)
    {
        int half = tableSize / 2;
        int cycle = tableSize - 1;
        int fixedPosition = tableSize - 1;

        List<List<PositionMatch>> rounds = new();

        for (int i = 0; i < cycle; i++)
        {
            int round = (LegStartRoundIndex + i) % cycle;

            // Round r pairs the positions whose sum is r (mod cycle). Start is the one that would pair with itself, so
            // it meets the fixed position.
            int start = (round * half) % cycle;
            List<PositionMatch> matches = new();

            // The fixed position alternates by the table's own round, not by i, so the rotated start keeps the classic
            // pattern.
            matches.Add(round % 2 == 0
                ? new PositionMatch(start, fixedPosition)
                : new PositionMatch(fixedPosition, start));

            for (int offset = 1; offset < half; offset++)
            {
                matches.Add(new PositionMatch((start + offset) % cycle, (start - offset + cycle) % cycle));
            }

            rounds.Add(matches);
        }

        return rounds;
    }

    /// <summary>
    /// Swaps home and away in every match of a round.
    /// </summary>
    /// <param name="round">The round to flip.</param>
    /// <returns>A new round with the same pairings and the venues swapped.</returns>
    private static List<PositionMatch> Flip(List<PositionMatch> round)
    {
        return round.Select(match => new PositionMatch(match.Away, match.Home)).ToList();
    }

    /// <summary>
    /// Drops the matches that involve one position.
    /// </summary>
    /// <param name="round">The round to filter.</param>
    /// <param name="position">The position whose matches are dropped.</param>
    /// <returns>A new round without those matches.</returns>
    private static List<PositionMatch> RemoveMatchesOf(List<PositionMatch> round, int position)
    {
        return round.Where(match => match.Home != position && match.Away != position).ToList();
    }
}
