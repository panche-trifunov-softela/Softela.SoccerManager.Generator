using System.Globalization;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Works out which ties of a continental cup bracket have both of their clubs known, and who hosts each leg.
/// </summary>
public static class ContinentalTieResolver
{
    /// <summary>
    /// The number of legs of the final, which is a single match.
    /// </summary>
    private const int FinalLegs = 1;

    /// <summary>
    /// The number of legs of every tie before the final, which is a home and an away match.
    /// </summary>
    private const int TwoLegs = 2;

    /// <summary>
    /// Lists the ties whose two clubs are known, given the winners decided so far, whether or not the ties have been
    /// played. With no winners these are the ties of round 1: the quarter-finals of a 16-club cup, or the play-off of
    /// a 32-club cup. With every winner they are all the ties of the bracket. In each tie the club placed higher in the
    /// league phase is the host, which hosts the second leg or the final.
    /// </summary>
    /// <param name="bracket">The bracket to read.</param>
    /// <param name="winners">The ties decided so far, with their winners.</param>
    /// <returns>The ties with both clubs known, in round order and then tie order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="bracket"/> or <paramref name="winners"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a winner is missing from the list, or names a round the bracket does not have. It is also thrown
    /// for the problems <see cref="KnockoutTieResolver.KnownTies"/> finds, and those messages speak of round numbers,
    /// not round names: a winner names a tie the round does not have, a tie has more than one winner, a tie has a
    /// winner before both of its clubs are known, or a winner is not one of the clubs of its tie.
    /// </exception>
    public static IReadOnlyList<ContinentalKnownTie> KnownTies(
        ContinentalBracket bracket,
        IReadOnlyList<ContinentalWinner> winners)
    {
        ArgumentNullException.ThrowIfNull(bracket);
        ArgumentNullException.ThrowIfNull(winners);

        int clubCount = bracket.Table.Count;
        IReadOnlyList<KnockoutKnownTie> knownTies = KnockoutTieResolver.KnownTies(
            bracket.Knockout,
            ToKnockoutWinners(clubCount, winners));

        Dictionary<int, int> placeOf = new();

        for (int place = 0; place < bracket.Table.Count; place++)
        {
            placeOf[bracket.Table[place]] = place;
        }

        List<ContinentalKnownTie> result = new();

        foreach (KnockoutKnownTie tie in knownTies)
        {
            ContinentalRound round = ContinentalBracketShape.RoundOf(clubCount, tie.RoundNumber);
            bool firstHosts = placeOf[tie.FirstLeagueTeamId] < placeOf[tie.SecondLeagueTeamId];
            int hostId = firstHosts ? tie.FirstLeagueTeamId : tie.SecondLeagueTeamId;
            int otherId = firstHosts ? tie.SecondLeagueTeamId : tie.FirstLeagueTeamId;
            int legs = round == ContinentalRound.Final ? FinalLegs : TwoLegs;

            result.Add(new ContinentalKnownTie(round, tie.TieNumber, hostId, otherId, legs));
        }

        return result;
    }

    /// <summary>
    /// Turns the winners into the winners of a knockout bracket, which names rounds by number.
    /// </summary>
    /// <param name="clubCount">The number of clubs in the cup.</param>
    /// <param name="winners">The ties decided so far, with their winners.</param>
    /// <returns>The same winners in the same order, with each round as a number.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a winner is missing from the list, or names a round the bracket does not have.
    /// </exception>
    private static List<KnockoutWinner> ToKnockoutWinners(int clubCount, IReadOnlyList<ContinentalWinner> winners)
    {
        List<KnockoutWinner> mapped = new();

        for (int index = 0; index < winners.Count; index++)
        {
            ContinentalWinner? winner = winners[index];

            if (winner is null)
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Winner {index} of the list is missing."),
                    nameof(winners));
            }

            if (!ContinentalBracketShape.TryGetRoundNumber(clubCount, winner.Round, out int roundNumber))
            {
                string roundText = Enum.IsDefined(winner.Round)
                    ? winner.Round.ToString()
                    : ((int)winner.Round).ToString(CultureInfo.InvariantCulture);

                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"The bracket has no round named {roundText}."),
                    nameof(winners));
            }

            mapped.Add(new KnockoutWinner(roundNumber, winner.TieNumber, winner.LeagueTeamId));
        }

        return mapped;
    }
}
