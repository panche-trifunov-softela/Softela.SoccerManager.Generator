using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Puts the clubs of a league in pyramid order from the previous season's final tables, for the continental cups of
/// Season 2 and later. The pyramid decides who enters cup 1 (its first 8 clubs) and cup 2 (the next 8).
/// </summary>
internal static class ContinentalPyramid
{
    /// <summary>
    /// Puts this season's division clubs in the order the previous season's tables gave them, and moves the previous
    /// season's domestic cup winner to the first place of cup 2 unless it is in cup 1 already. The order is:
    /// <list type="number">
    /// <item>
    /// <description>
    /// the clubs of <paramref name="tableOrder"/> that are in <paramref name="seasonOnePyramid"/>, in the order of
    /// <paramref name="tableOrder"/>, so a club that moved to another division counts where it finished;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// the clubs of <paramref name="seasonOnePyramid"/> that are not in <paramref name="tableOrder"/>, in their order,
    /// so a club with no place in the previous tables comes after the others, in the order the pyramid has without
    /// tables;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// the cup winner, when it is in the list at the first place of cup 2 or later, moved to the first place of cup 2.
    /// A winner in cup 1, or one that is not in the list, changes nothing.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="tableOrder">
    /// The previous season's division tables joined in division order, place 1 first.
    /// </param>
    /// <param name="seasonOnePyramid">
    /// The clubs of this season's divisions in the order of the Season 1 rule, which is the order for clubs that the
    /// previous tables do not list.
    /// </param>
    /// <param name="cupWinner">The league team id of the previous season's domestic cup winner, or null.</param>
    /// <returns>The league team ids of every club of <paramref name="seasonOnePyramid"/>, in pyramid order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tableOrder"/> or <paramref name="seasonOnePyramid"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a league team id is listed more than once in either list.
    /// </exception>
    internal static IReadOnlyList<int> FromPreviousSeason(
        IReadOnlyList<int> tableOrder,
        IReadOnlyList<int> seasonOnePyramid,
        int? cupWinner)
    {
        ArgumentNullException.ThrowIfNull(tableOrder);
        ArgumentNullException.ThrowIfNull(seasonOnePyramid);

        RefuseDuplicates(tableOrder, nameof(tableOrder));
        RefuseDuplicates(seasonOnePyramid, nameof(seasonOnePyramid));

        HashSet<int> thisSeason = seasonOnePyramid.ToHashSet();
        HashSet<int> previousTables = tableOrder.ToHashSet();

        List<int> pyramid = tableOrder
            .Where(thisSeason.Contains)
            .Concat(seasonOnePyramid.Where(club => !previousTables.Contains(club)))
            .ToList();

        if (cupWinner is not { } winner)
        {
            return pyramid;
        }

        int index = pyramid.IndexOf(winner);

        // At the first place of cup 2 the winner is there already; in cup 1 or outside the list nothing moves.
        if (index > ContinentalEntrantSelector.LeagueClubsPerCup)
        {
            pyramid.RemoveAt(index);
            pyramid.Insert(ContinentalEntrantSelector.LeagueClubsPerCup, winner);
        }

        return pyramid;
    }

    /// <summary>
    /// Refuses a league team id that is listed more than once.
    /// </summary>
    /// <param name="clubs">The league team ids.</param>
    /// <param name="parameterName">The name of the list, for the exception.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a league team id is listed more than once. The first club found a second time is the one named.
    /// </exception>
    private static void RefuseDuplicates(IReadOnlyList<int> clubs, string parameterName)
    {
        HashSet<int> seen = new();

        foreach (int clubId in clubs)
        {
            if (!seen.Add(clubId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Club {clubId} is listed more than once."),
                    parameterName);
            }
        }
    }
}
