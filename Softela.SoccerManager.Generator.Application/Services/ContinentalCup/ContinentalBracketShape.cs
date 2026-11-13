using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// The one owner of a continental cup bracket's wiring and round numbering. The pairings are UEFA's Champions League
/// bracket (2026/27 regulations, Article 19). UEFA draws which club of each pair of places goes into which half of the
/// bracket; this bracket fixes it by place instead, so nothing in it is drawn.
/// </summary>
/// <remarks>
/// The wiring is data. A tie is a pair of numbers, one per side. A positive number is a league-phase place, so 3 is
/// the club in place 3. A negative number is the winner of a tie of the round before, so -2 is the winner of tie 2 of
/// the round before. Every later round only takes winners of the round before it.
/// </remarks>
internal static class ContinentalBracketShape
{
    /// <summary>
    /// The rounds of a 16-club cup, from round 1 to the final.
    /// </summary>
    private static readonly ContinentalRound[] SmallRounds =
    {
        ContinentalRound.QuarterFinal,
        ContinentalRound.SemiFinal,
        ContinentalRound.Final,
    };

    /// <summary>
    /// The rounds of a 32-club cup, from round 1 to the final.
    /// </summary>
    private static readonly ContinentalRound[] LargeRounds =
    {
        ContinentalRound.PlayOff,
        ContinentalRound.RoundOf16,
        ContinentalRound.QuarterFinal,
        ContinentalRound.SemiFinal,
        ContinentalRound.Final,
    };

    /// <summary>
    /// The ties of a 16-club cup: round by round, each tie as its two sides.
    /// </summary>
    private static readonly int[][][] SmallWiring =
    {
        new[] { new[] { 1, 8 }, new[] { 4, 5 }, new[] { 2, 7 }, new[] { 3, 6 } },
        new[] { new[] { -1, -2 }, new[] { -3, -4 } },
        new[] { new[] { -1, -2 } },
    };

    /// <summary>
    /// The ties of a 32-club cup: round by round, each tie as its two sides.
    /// </summary>
    private static readonly int[][][] LargeWiring =
    {
        new[]
        {
            new[] { 9, 23 },
            new[] { 11, 21 },
            new[] { 13, 19 },
            new[] { 15, 17 },
            new[] { 10, 24 },
            new[] { 12, 22 },
            new[] { 14, 20 },
            new[] { 16, 18 },
        },
        new[]
        {
            new[] { 5, -2 },
            new[] { 3, -3 },
            new[] { 7, -1 },
            new[] { 1, -4 },
            new[] { 6, -6 },
            new[] { 4, -7 },
            new[] { 8, -5 },
            new[] { 2, -8 },
        },
        new[] { new[] { -1, -2 }, new[] { -3, -4 }, new[] { -5, -6 }, new[] { -7, -8 } },
        new[] { new[] { -1, -2 }, new[] { -3, -4 } },
        new[] { new[] { -1, -2 } },
    };

    /// <summary>
    /// Builds the knockout bracket for a table. Nothing is drawn: every tie follows from the places.
    /// </summary>
    /// <param name="table">
    /// The league team ids in place order, place 1 first; the caller has checked that there are 16 or 32 different ids.
    /// </param>
    /// <returns>The bracket; round r is at position r - 1 and tie t is at position t - 1.</returns>
    internal static KnockoutBracket Build(IReadOnlyList<int> table)
    {
        int[][][] wiring = table.Count == ContinentalInputValidator.SmallCupClubCount ? SmallWiring : LargeWiring;
        List<IReadOnlyList<KnockoutTie>> rounds = new();

        for (int round = 1; round <= wiring.Length; round++)
        {
            List<KnockoutTie> ties = new();
            int[][] roundWiring = wiring[round - 1];

            for (int tie = 1; tie <= roundWiring.Length; tie++)
            {
                int[] sides = roundWiring[tie - 1];

                ties.Add(new KnockoutTie(
                    round,
                    tie,
                    ToSide(sides[0], round, table),
                    ToSide(sides[1], round, table)));
            }

            rounds.Add(ties);
        }

        return new KnockoutBracket(rounds);
    }

    /// <summary>
    /// Finds the round that has a given number in a cup of a given size.
    /// </summary>
    /// <param name="clubCount">The number of clubs in the cup: 16 or 32.</param>
    /// <param name="roundNumber">The 1-based number of the round.</param>
    /// <returns>The round.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the club count is not 16 or 32, or the cup of that size has no such round number.
    /// </exception>
    internal static ContinentalRound RoundOf(int clubCount, int roundNumber)
    {
        ContinentalRound[]? rounds = RoundsOf(clubCount);

        if (rounds is null)
        {
            throw new ArgumentOutOfRangeException(nameof(clubCount), clubCount, "The club count is not 16 or 32.");
        }

        if (roundNumber < 1 || roundNumber > rounds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber), roundNumber, "The cup has no such round.");
        }

        return rounds[roundNumber - 1];
    }

    /// <summary>
    /// Finds the number of a round in a cup of a given size.
    /// </summary>
    /// <param name="clubCount">The number of clubs in the cup: 16 or 32.</param>
    /// <param name="round">The round to look for.</param>
    /// <param name="roundNumber">The 1-based number of the round, or 0 when the cup has no such round.</param>
    /// <returns>
    /// True when the cup has the round. False when it does not: the play-off and the round of 16 in a 16-club cup, an
    /// undefined value, or a club count that is not 16 or 32.
    /// </returns>
    internal static bool TryGetRoundNumber(int clubCount, ContinentalRound round, out int roundNumber)
    {
        ContinentalRound[]? rounds = RoundsOf(clubCount);
        int index = rounds is null ? -1 : Array.IndexOf(rounds, round);

        roundNumber = index + 1;

        return index >= 0;
    }

    /// <summary>
    /// Finds the rounds of a cup of a given size.
    /// </summary>
    /// <param name="clubCount">The number of clubs in the cup.</param>
    /// <returns>The rounds from round 1 to the final, or null when the club count is not 16 or 32.</returns>
    private static ContinentalRound[]? RoundsOf(int clubCount)
    {
        return clubCount switch
        {
            ContinentalInputValidator.SmallCupClubCount => SmallRounds,
            ContinentalInputValidator.LargeCupClubCount => LargeRounds,
            _ => null,
        };
    }

    /// <summary>
    /// Turns one number of the wiring into a side of a tie.
    /// </summary>
    /// <param name="code">The number: a place when positive, or minus the earlier tie whose winner it is.</param>
    /// <param name="roundNumber">The 1-based round of the tie that the side belongs to.</param>
    /// <param name="table">The league team ids in place order, place 1 first.</param>
    /// <returns>The side: the club in that place, or the winner of that tie of the round before.</returns>
    private static KnockoutSide ToSide(int code, int roundNumber, IReadOnlyList<int> table)
    {
        if (code > 0)
        {
            return KnockoutSide.Club(table[code - 1]);
        }

        return KnockoutSide.WinnerOf(roundNumber - 1, -code);
    }
}
