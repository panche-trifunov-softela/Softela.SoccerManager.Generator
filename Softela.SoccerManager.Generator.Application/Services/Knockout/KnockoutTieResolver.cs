using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.Knockout;

/// <summary>
/// Works out which ties of a bracket have both of their clubs known.
/// </summary>
public static class KnockoutTieResolver
{
    private const string WinnersParamName = "winners";

    /// <summary>
    /// Lists the ties whose two clubs are known, given the winners decided so far, whether or not the ties have been
    /// played. With no winners these are the ties of round 1 and any later tie whose two sides are both clubs; with
    /// every winner they are all the ties of the bracket.
    /// </summary>
    /// <param name="bracket">The bracket to read.</param>
    /// <param name="winners">The ties decided so far, with their winners.</param>
    /// <returns>The ties with both clubs known, in round order and then tie order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="bracket"/> or <paramref name="winners"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a winner names a round or tie the bracket does not have, a tie has more than one winner, a tie has
    /// a winner before both of its clubs are known, or a winner is not one of the clubs of its tie.
    /// </exception>
    public static IReadOnlyList<KnockoutKnownTie> KnownTies(
        KnockoutBracket bracket,
        IReadOnlyList<KnockoutWinner> winners)
    {
        ArgumentNullException.ThrowIfNull(bracket);
        ArgumentNullException.ThrowIfNull(winners);

        Dictionary<(int Round, int Tie), int> winnerOf = IndexWinners(bracket, winners);
        List<KnockoutKnownTie> known = new();

        foreach (IReadOnlyList<KnockoutTie> round in bracket.Rounds)
        {
            foreach (KnockoutTie tie in round)
            {
                int? first = ResolveSide(tie.First, winnerOf);
                int? second = ResolveSide(tie.Second, winnerOf);

                if (first.HasValue && second.HasValue)
                {
                    known.Add(new KnockoutKnownTie(tie.RoundNumber, tie.TieNumber, first.Value, second.Value));
                }

                if (winnerOf.TryGetValue((tie.RoundNumber, tie.TieNumber), out int winnerId))
                {
                    CheckWinner(tie, first, second, winnerId);
                }
            }
        }

        return known;
    }

    /// <summary>
    /// Indexes the winners by round and tie, refusing any that cannot belong to the bracket.
    /// </summary>
    /// <param name="bracket">The bracket the winners belong to.</param>
    /// <param name="winners">The ties decided so far, with their winners.</param>
    /// <returns>The league team id of the winner, by round and tie.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a winner names a round or tie the bracket does not have, or a tie has more than one winner.
    /// </exception>
    private static Dictionary<(int Round, int Tie), int> IndexWinners(
        KnockoutBracket bracket,
        IReadOnlyList<KnockoutWinner> winners)
    {
        Dictionary<(int Round, int Tie), int> winnerOf = new();

        foreach (KnockoutWinner winner in winners)
        {
            if (winner.RoundNumber < 1 || winner.RoundNumber > bracket.Rounds.Count)
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"The bracket has no round {winner.RoundNumber}."),
                    nameof(winners));
            }

            if (winner.TieNumber < 1 || winner.TieNumber > bracket.Rounds[winner.RoundNumber - 1].Count)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Round {winner.RoundNumber} has no tie {winner.TieNumber}."),
                    nameof(winners));
            }

            if (!winnerOf.TryAdd((winner.RoundNumber, winner.TieNumber), winner.LeagueTeamId))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Tie {winner.TieNumber} of round {winner.RoundNumber} has more than one winner."),
                    nameof(winners));
            }
        }

        return winnerOf;
    }

    /// <summary>
    /// Finds the club on one side of a tie.
    /// </summary>
    /// <param name="side">The side to resolve.</param>
    /// <param name="winnerOf">The league team id of the winner, by round and tie.</param>
    /// <returns>The club's league team id, or null when the side is the winner of a tie not yet decided.</returns>
    private static int? ResolveSide(KnockoutSide side, Dictionary<(int Round, int Tie), int> winnerOf)
    {
        if (side.IsClub)
        {
            return side.LeagueTeamId;
        }

        if (winnerOf.TryGetValue((side.WinnerOfRoundNumber!.Value, side.WinnerOfTieNumber!.Value), out int winnerId))
        {
            return winnerId;
        }

        return null;
    }

    /// <summary>
    /// Checks that a decided tie had both of its clubs known and that its winner is one of them.
    /// </summary>
    /// <param name="tie">The decided tie.</param>
    /// <param name="first">The club on the tie's first side, or null when it is not known.</param>
    /// <param name="second">The club on the tie's second side, or null when it is not known.</param>
    /// <param name="winnerId">The league team id of the tie's winner.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a side is not known, or the winner is neither of the tie's clubs.
    /// </exception>
    private static void CheckWinner(KnockoutTie tie, int? first, int? second, int winnerId)
    {
        int round = tie.RoundNumber;
        int number = tie.TieNumber;

        if (!first.HasValue || !second.HasValue)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Tie {number} of round {round} has a winner before both of its clubs are known."),
                WinnersParamName);
        }

        if (winnerId != first.Value && winnerId != second.Value)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Club {winnerId} cannot win tie {number} of round {round}, because it does not play in it."),
                WinnersParamName);
        }
    }
}
