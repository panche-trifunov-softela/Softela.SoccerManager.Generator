using System.Globalization;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Chooses the entrants of the two continental cups. Cup 1 takes the first 8 clubs of the league's pyramid and cup 2
/// the next 8; there is no domestic cup winner yet. The guests fill the places the league clubs leave empty, the
/// strongest first and cup 1 first. Each cup's list is in pot order: the draw cuts it into quarters, so the first
/// quarter is pot 1.
/// </summary>
internal static class ContinentalEntrantSelector
{
    /// <summary>
    /// The number of league clubs each cup takes from the pyramid.
    /// </summary>
    private const int LeagueClubsPerCup = 8;

    /// <summary>
    /// Puts the clubs of the divisions in pyramid order: Division 1 first, and inside each division the strongest squad
    /// first. This is the Season 1 rule, and it is used for every season until a later step passes the previous
    /// season's tables; <see cref="Select"/> takes any order.
    /// </summary>
    /// <param name="divisionsInOrder">The squad strengths of each division's clubs, Division 1 first.</param>
    /// <returns>The league team ids of every club of every division, in pyramid order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="divisionsInOrder"/> or one of its divisions is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a league team id is listed more than once in a division.
    /// </exception>
    public static IReadOnlyList<int> SeasonOnePyramid(IReadOnlyList<IReadOnlyList<SquadStrength>> divisionsInOrder)
    {
        ArgumentNullException.ThrowIfNull(divisionsInOrder);

        List<int> pyramid = new();

        foreach (IReadOnlyList<SquadStrength> division in divisionsInOrder)
        {
            ArgumentNullException.ThrowIfNull(division, nameof(divisionsInOrder));

            pyramid.AddRange(SquadStrengthRanking.StrongestFirst(division).Select(club => club.LeagueTeamId));
        }

        return pyramid;
    }

    /// <summary>
    /// Ranks the guests from the strongest squad to the weakest, in the order
    /// <see cref="SquadStrengthRanking.Compare"/> gives: a higher total of the best 16, then a higher 17th rating,
    /// then a lower team id, then a lower league team id.
    /// </summary>
    /// <param name="guests">The squad strengths of the guests.</param>
    /// <returns>The league team ids of the guests, strongest first.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="guests"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a league team id is listed more than once.</exception>
    public static IReadOnlyList<int> RankGuests(IReadOnlyList<SquadStrength> guests)
    {
        ArgumentNullException.ThrowIfNull(guests);

        return SquadStrengthRanking.StrongestFirst(guests).Select(club => club.LeagueTeamId).ToList();
    }

    /// <summary>
    /// Chooses the entrants of both cups. Cup 1 takes the first 8 clubs of the pyramid, or all of them when there are
    /// fewer, and cup 2 takes the next 8, or what is left of them. Each cup is filled up to its size with guests: cup 1
    /// takes the first guests of the ranking and cup 2 the next ones. When the ranking is shorter than the two cups
    /// need together, nobody is chosen.
    /// </summary>
    /// <param name="pyramid">The league team ids of the league clubs, strongest first.</param>
    /// <param name="rankedGuests">The league team ids of the guests, strongest first.</param>
    /// <param name="cupSize">The number of clubs in each cup: 16 or 32.</param>
    /// <returns>
    /// The entrants of both cups in pot order, or the number of guests needed and the number found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pyramid"/> or <paramref name="rankedGuests"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="cupSize"/> is not 16 or 32.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a league team id is listed more than once, in one list or across both.
    /// </exception>
    public static ContinentalEntrantSelection Select(
        IReadOnlyList<int> pyramid,
        IReadOnlyList<int> rankedGuests,
        int cupSize)
    {
        ArgumentNullException.ThrowIfNull(pyramid);
        ArgumentNullException.ThrowIfNull(rankedGuests);

        if (cupSize != ContinentalInputValidator.SmallCupClubCount
            && cupSize != ContinentalInputValidator.LargeCupClubCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cupSize),
                cupSize,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A continental cup has {ContinentalInputValidator.SmallCupClubCount} or "
                    + $"{ContinentalInputValidator.LargeCupClubCount} clubs."));
        }

        RefuseDuplicates(pyramid, rankedGuests);

        List<int> cup1 = pyramid.Take(LeagueClubsPerCup).ToList();
        List<int> cup2 = pyramid.Skip(LeagueClubsPerCup).Take(LeagueClubsPerCup).ToList();

        int need1 = cupSize - cup1.Count;
        int need2 = cupSize - cup2.Count;
        int needed = need1 + need2;

        if (rankedGuests.Count < needed)
        {
            return new ContinentalEntrantSelection(
                ContinentalEntrantSelectionStatus.TooFewGuests,
                [],
                [],
                needed,
                rankedGuests.Count);
        }

        cup1.AddRange(rankedGuests.Take(need1));
        cup2.AddRange(rankedGuests.Skip(need1).Take(need2));

        return new ContinentalEntrantSelection(
            ContinentalEntrantSelectionStatus.Selected,
            cup1,
            cup2,
            needed,
            rankedGuests.Count);
    }

    /// <summary>
    /// Refuses a league team id that is listed more than once, in one list or across both.
    /// </summary>
    /// <param name="pyramid">The league team ids of the league clubs.</param>
    /// <param name="rankedGuests">The league team ids of the guests.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a league team id is listed more than once. The first club found a second time, in the order
    /// pyramid then guests, is the one named.
    /// </exception>
    private static void RefuseDuplicates(IReadOnlyList<int> pyramid, IReadOnlyList<int> rankedGuests)
    {
        HashSet<int> seen = new();

        foreach (int clubId in pyramid.Concat(rankedGuests))
        {
            if (!seen.Add(clubId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Club {clubId} is listed more than once."),
                    nameof(rankedGuests));
            }
        }
    }
}
