using System.Globalization;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Builds the knockout bracket of the World Cup finals from the final tables of the 8 groups: 16 nations in a round of
/// 16, then quarter-finals, semi-finals and the final. The pairing is the 2022 World Cup's, so two nations of one group
/// can meet again only in the final. The third-place match is not a round of the bracket.
/// </summary>
internal static class FinalsBracket
{
    /// <summary>
    /// The pairs of the round of 16, in tie order, as the group of the winner and the group of the runner-up, counting
    /// the groups from 0: 1A v 2B, 1C v 2D, 1E v 2F, 1G v 2H, 1B v 2A, 1D v 2C, 1F v 2E and 1H v 2G.
    /// </summary>
    private static readonly (int WinnerGroup, int RunnerUpGroup)[] FirstRoundPairs =
    [
        (0, 1),
        (2, 3),
        (4, 5),
        (6, 7),
        (1, 0),
        (3, 2),
        (5, 4),
        (7, 6),
    ];

    /// <summary>
    /// Builds the bracket. Round 1 has the 8 ties of the round of 16, each with the group winner as its first side, and
    /// every later round takes the winners of two neighbouring ties of the round before: tie t of a round is the
    /// winners of ties 2t - 1 and 2t before it.
    /// </summary>
    /// <param name="groupTables">
    /// The final table of each of the 8 groups, in group order (A first), each with its 4 league team ids in place
    /// order.
    /// </param>
    /// <returns>The bracket, with 4 rounds of 8, 4, 2 and 1 ties.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groupTables"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are not 8 tables of 4 nations, or a nation is in more than one place.
    /// </exception>
    internal static KnockoutBracket Build(IReadOnlyList<IReadOnlyList<int>> groupTables)
    {
        ArgumentNullException.ThrowIfNull(groupTables);

        CheckTables(groupTables);

        List<IReadOnlyList<KnockoutTie>> rounds = [BuildFirstRound(groupTables)];
        int tieCount = FirstRoundPairs.Length / 2;

        for (int round = 2; round <= FinalsStages.BracketNumberOf(FinalsRound.Final); round++)
        {
            rounds.Add(KnockoutLaterRound.Build(round, tieCount));

            tieCount /= 2;
        }

        return new KnockoutBracket(rounds);
    }

    /// <summary>
    /// Checks that the tables are the 8 groups of 4 distinct nations.
    /// </summary>
    /// <param name="groupTables">The final table of each group.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when there are not 8 tables of 4 nations, or a nation is in more than one place.
    /// </exception>
    private static void CheckTables(IReadOnlyList<IReadOnlyList<int>> groupTables)
    {
        if (groupTables.Count != WorldCupFinalsShape.GroupCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The finals have {WorldCupFinalsShape.GroupCount} groups, but {groupTables.Count} tables ")
                + "were given.",
                nameof(groupTables));
        }

        for (int index = 0; index < groupTables.Count; index++)
        {
            if (groupTables[index].Count != WorldCupFinalsShape.GroupSize)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The table of group {index + 1} has {groupTables[index].Count} nations, but a group has ")
                    + string.Create(CultureInfo.InvariantCulture, $"{WorldCupFinalsShape.GroupSize}."),
                    nameof(groupTables));
            }
        }

        if (groupTables.SelectMany(table => table).Distinct().Count() != WorldCupFinalsShape.NationCount)
        {
            throw new ArgumentException("A nation is in more than one place of the group tables.", nameof(groupTables));
        }
    }

    /// <summary>
    /// Builds the round of 16.
    /// </summary>
    /// <param name="groupTables">The final table of each group, which have been checked.</param>
    /// <returns>The 8 ties, numbered from 1, each with its group winner as the first side.</returns>
    private static IReadOnlyList<KnockoutTie> BuildFirstRound(IReadOnlyList<IReadOnlyList<int>> groupTables)
    {
        List<KnockoutTie> ties = [];

        for (int index = 0; index < FirstRoundPairs.Length; index++)
        {
            (int winnerGroup, int runnerUpGroup) = FirstRoundPairs[index];

            ties.Add(new KnockoutTie(
                1,
                index + 1,
                KnockoutSide.Club(groupTables[winnerGroup][0]),
                KnockoutSide.Club(groupTables[runnerUpGroup][1])));
        }

        return ties;
    }
}
