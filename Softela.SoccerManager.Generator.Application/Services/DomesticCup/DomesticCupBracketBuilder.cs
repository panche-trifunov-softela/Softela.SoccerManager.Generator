using Softela.SoccerManager.Generator.Application.Core.Randomness;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Chooses the clubs that skip round 1 of a domestic cup and builds the cup's bracket.
/// </summary>
public static class DomesticCupBracketBuilder
{
    private const int FirstRound = 1;
    private const int SecondRound = 2;

    /// <summary>
    /// Chooses the clubs that skip round 1: the first ones in the given order, as many as the bracket needs. The caller
    /// orders the entrants by division order, top division first, and then by squad strength, strongest first, as
    /// <see cref="SquadStrengthRanking.Compare"/> orders it. This is used once, when the cup has no match yet, because
    /// after that the bye clubs are read back from the written round 1.
    /// </summary>
    /// <param name="entrantsInByeOrder">The league team ids of the clubs that enter the cup, in bye order.</param>
    /// <returns>A new list of the bye clubs; it is empty when the club count is a power of two.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entrantsInByeOrder"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are fewer than 2 entrants, or a club is listed more than once.
    /// </exception>
    public static IReadOnlyList<int> ChooseByes(IReadOnlyList<int> entrantsInByeOrder)
    {
        DomesticCupInputValidator.ValidateEntrants(entrantsInByeOrder, nameof(entrantsInByeOrder));

        int byeCount = DomesticCupShape.ByeCount(entrantsInByeOrder.Count);

        return entrantsInByeOrder.Take(byeCount).ToList();
    }

    /// <summary>
    /// Builds the bracket of a cup. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// It depends only on the random source's state and on the sets of entrants and bye clubs, never on the order of
    /// the lists.
    /// </description>
    /// </item>
    /// <item><description>The caller's lists are not changed.</description></item>
    /// <item>
    /// <description>
    /// It draws from the random source (three shuffles), so the caller passes a source derived for this purpose only.
    /// </description>
    /// </item>
    /// <item><description>There are one fewer ties than entrants in all.</description></item>
    /// <item>
    /// <description>
    /// Every entrant plays exactly one first tie: in round 1 for a club without a bye, and in round 2 for a bye club.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="entrants">The league team ids of the clubs that enter the cup.</param>
    /// <param name="byeClubs">The league team ids of the clubs that skip round 1; they must be entrants.</param>
    /// <param name="random">The random source; its state advances.</param>
    /// <returns>The bracket, with round 1 first and the final last.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entrants"/>, <paramref name="byeClubs"/> or <paramref name="random"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are fewer than 2 entrants, a club is listed twice, or the bye clubs are not entrants or are
    /// not as many as the bracket needs.
    /// </exception>
    public static KnockoutBracket Build(
        IReadOnlyList<int> entrants,
        IReadOnlyList<int> byeClubs,
        SeededRandom random)
    {
        DomesticCupInputValidator.ValidateEntrants(entrants, nameof(entrants));
        DomesticCupInputValidator.ValidateByes(entrants, byeClubs);
        ArgumentNullException.ThrowIfNull(random);

        int roundCount = DomesticCupShape.RoundCount(entrants.Count);
        int firstRoundTies = DomesticCupShape.FirstRoundTieCount(entrants.Count);

        // Sorted copies, so the order the caller's lists arrived in cannot change the result.
        List<int> byes = byeClubs.OrderBy(clubId => clubId).ToList();
        HashSet<int> byeSet = new(byeClubs);
        List<int> others = entrants.Where(clubId => !byeSet.Contains(clubId)).OrderBy(clubId => clubId).ToList();

        // The order of the three shuffles is fixed: the clubs without a bye, the bye clubs, the round 2 pairs.
        random.Shuffle(others);

        List<List<KnockoutTie>> rounds = new() { BuildFirstRound(others, firstRoundTies) };

        if (roundCount == 1)
        {
            return ToBracket(rounds);
        }

        random.Shuffle(byes);

        rounds.Add(BuildSecondRound(byes, firstRoundTies, random));

        for (int round = 3; round <= roundCount; round++)
        {
            rounds.Add(BuildLaterRound(round, 1 << (roundCount - round)));
        }

        return ToBracket(rounds);
    }

    /// <summary>
    /// Pairs the shuffled clubs without a bye into round 1.
    /// </summary>
    /// <param name="shuffledOthers">The clubs without a bye, already shuffled.</param>
    /// <param name="tieCount">The number of round 1 ties.</param>
    /// <returns>The round 1 ties, numbered from 1.</returns>
    private static List<KnockoutTie> BuildFirstRound(List<int> shuffledOthers, int tieCount)
    {
        List<KnockoutTie> ties = new();

        for (int tie = 1; tie <= tieCount; tie++)
        {
            ties.Add(new KnockoutTie(
                FirstRound,
                tie,
                KnockoutSide.Club(shuffledOthers[(2 * tie) - 2]),
                KnockoutSide.Club(shuffledOthers[(2 * tie) - 1])));
        }

        return ties;
    }

    /// <summary>
    /// Builds round 2: each bye club meets a round 1 winner while there are any, and the clubs left over, whether bye
    /// clubs or round 1 winners, meet each other. The pairs are shuffled before they are numbered, so no bye club is
    /// tied to a place in the bracket.
    /// </summary>
    /// <param name="shuffledByes">The bye clubs, already shuffled.</param>
    /// <param name="firstRoundTies">The number of round 1 ties.</param>
    /// <param name="random">The random source; its state advances.</param>
    /// <returns>The round 2 ties, numbered from 1.</returns>
    private static List<KnockoutTie> BuildSecondRound(
        List<int> shuffledByes,
        int firstRoundTies,
        SeededRandom random)
    {
        List<(KnockoutSide First, KnockoutSide Second)> pairs = new();
        int paired = Math.Min(shuffledByes.Count, firstRoundTies);

        for (int i = 1; i <= paired; i++)
        {
            pairs.Add((KnockoutSide.Club(shuffledByes[i - 1]), KnockoutSide.WinnerOf(FirstRound, i)));
        }

        // Only one of the two loops below can add pairs: byes and round 1 ties together fill half the bracket, so
        // whichever is more numerous exceeds the other by an even number.
        for (int j = paired; j < shuffledByes.Count - 1; j += 2)
        {
            pairs.Add((KnockoutSide.Club(shuffledByes[j]), KnockoutSide.Club(shuffledByes[j + 1])));
        }

        for (int j = paired + 1; j < firstRoundTies; j += 2)
        {
            pairs.Add((KnockoutSide.WinnerOf(FirstRound, j), KnockoutSide.WinnerOf(FirstRound, j + 1)));
        }

        random.Shuffle(pairs);

        List<KnockoutTie> ties = new();

        for (int tie = 1; tie <= pairs.Count; tie++)
        {
            ties.Add(new KnockoutTie(SecondRound, tie, pairs[tie - 1].First, pairs[tie - 1].Second));
        }

        return ties;
    }

    /// <summary>
    /// Builds a round after round 2: each tie takes the winners of two neighbouring ties of the round before.
    /// </summary>
    /// <param name="round">The 1-based number of the round; it is at least 3.</param>
    /// <param name="tieCount">The number of ties in the round.</param>
    /// <returns>The ties of the round, numbered from 1.</returns>
    private static List<KnockoutTie> BuildLaterRound(int round, int tieCount)
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

    /// <summary>
    /// Wraps the built rounds in a bracket.
    /// </summary>
    /// <param name="rounds">The rounds, round 1 first.</param>
    /// <returns>The bracket.</returns>
    private static KnockoutBracket ToBracket(List<List<KnockoutTie>> rounds)
    {
        return new KnockoutBracket(rounds.Select(round => (IReadOnlyList<KnockoutTie>)round).ToList());
    }
}
