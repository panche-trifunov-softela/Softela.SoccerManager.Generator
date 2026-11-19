namespace Softela.SoccerManager.Generator.Application.Services.Knockout;

/// <summary>
/// Builds a round of a knockout bracket whose ties take the winners of the round before: the later rounds of a domestic
/// cup and of the World Cup finals.
/// </summary>
public static class KnockoutLaterRound
{
    /// <summary>
    /// Builds a round that follows another: each tie takes the winners of two neighbouring ties of the round before.
    /// </summary>
    /// <param name="round">The 1-based number of the round; the round before it is <c>round - 1</c>.</param>
    /// <param name="tieCount">The number of ties in the round.</param>
    /// <returns>The ties of the round, numbered from 1.</returns>
    public static IReadOnlyList<KnockoutTie> Build(int round, int tieCount)
    {
        List<KnockoutTie> ties = new();

        for (int tie = 1; tie <= tieCount; tie++)
        {
            ties.Add(new KnockoutTie(
                round,
                tie,
                KnockoutSide.WinnerOf(round - 1, (2 * tie) - 1),
                KnockoutSide.WinnerOf(round - 1, 2 * tie)));
        }

        return ties;
    }
}
