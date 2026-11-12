using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Measures and ranks the squads of the clubs in a domestic cup, so the cup can give byes to the strongest clubs and
/// play a tie at the ground of the weaker one.
/// </summary>
public static class SquadStrengthRanking
{
    /// <summary>
    /// The number of best players that make a squad's strength. The domestic cup step also uses it as the number of
    /// rated players a full squad needs.
    /// </summary>
    internal const int BestCount = 16;

    /// <summary>
    /// Measures a club's squad. Each player counts once, however many rows the player has, and a missing rating
    /// counts as 0. The average of the best 16 is the total over 16 in every case, so comparing totals compares
    /// averages.
    /// </summary>
    /// <param name="leagueTeamId">The league team id of the club.</param>
    /// <param name="teamId">The id of the club's team row.</param>
    /// <param name="players">The rows of the squad read; a player may have one row for each position.</param>
    /// <returns>
    /// The sum of the 16 best ratings, with missing places counting 0, and the 17th best rating, or 0 when there is
    /// none.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="players"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a player is listed with two different ratings; a missing rating and a number count as different.
    /// </exception>
    public static SquadStrength Measure(int leagueTeamId, int teamId, IReadOnlyList<SquadPlayerRating> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        Dictionary<int, int?> ratingOf = new();

        foreach (SquadPlayerRating row in players)
        {
            if (ratingOf.TryGetValue(row.PlayerId, out int? known))
            {
                if (known != row.Rating)
                {
                    throw new ArgumentException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Player {row.PlayerId} is listed with two different ratings."),
                        nameof(players));
                }

                continue;
            }

            ratingOf[row.PlayerId] = row.Rating;
        }

        List<int> ratings = ratingOf.Values.Select(rating => rating ?? 0).OrderByDescending(rating => rating).ToList();

        long total = ratings.Take(BestCount).Sum(rating => (long)rating);
        int seventeenth = ratings.Count > BestCount ? ratings[BestCount] : 0;

        return new SquadStrength(leagueTeamId, teamId, total, seventeenth);
    }

    /// <summary>
    /// Compares two clubs by strength. The order is a higher total of the best 16, then a higher 17th rating, then a
    /// lower team id, then a lower league team id. A team id is unique within a league, because the league's teams
    /// have a unique league and team key, so the last key only keeps the order total: the result is never 0 for two
    /// different clubs.
    /// </summary>
    /// <param name="first">The first club.</param>
    /// <param name="second">The second club.</param>
    /// <returns>
    /// A negative number when <paramref name="first"/> is stronger, a positive one when <paramref name="second"/>
    /// is, and 0 only when both are the same club.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="first"/> or <paramref name="second"/> is null.
    /// </exception>
    public static int Compare(SquadStrength first, SquadStrength second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        int byTotal = second.BestSixteenTotal.CompareTo(first.BestSixteenTotal);

        if (byTotal != 0)
        {
            return byTotal;
        }

        int bySeventeenth = second.SeventeenthRating.CompareTo(first.SeventeenthRating);

        if (bySeventeenth != 0)
        {
            return bySeventeenth;
        }

        int byTeam = first.TeamId.CompareTo(second.TeamId);

        if (byTeam != 0)
        {
            return byTeam;
        }

        return first.LeagueTeamId.CompareTo(second.LeagueTeamId);
    }

    /// <summary>
    /// Sorts clubs from the strongest to the weakest, in the order <see cref="Compare"/> gives.
    /// </summary>
    /// <param name="clubs">The clubs to sort.</param>
    /// <returns>A new sorted list; the input is not changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clubs"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a league team id is listed more than once.</exception>
    public static IReadOnlyList<SquadStrength> StrongestFirst(IEnumerable<SquadStrength> clubs)
    {
        ArgumentNullException.ThrowIfNull(clubs);

        List<SquadStrength> sorted = clubs.ToList();
        HashSet<int> seen = new();

        foreach (SquadStrength club in sorted)
        {
            if (!seen.Add(club.LeagueTeamId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Club {club.LeagueTeamId} is listed more than once."),
                    nameof(clubs));
            }
        }

        sorted.Sort(Compare);

        return sorted;
    }

    /// <summary>
    /// Chooses which of two clubs plays a tie at home: the weaker one, which is the one <see cref="Compare"/> puts
    /// second. The answer is the same whichever way round the clubs are given.
    /// </summary>
    /// <param name="first">One club of the tie.</param>
    /// <param name="second">The other club of the tie.</param>
    /// <returns>The league team id of the weaker club.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="first"/> or <paramref name="second"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when both sides are the same club.</exception>
    public static int WeakerAtHome(SquadStrength first, SquadStrength second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.LeagueTeamId == second.LeagueTeamId)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Club {first.LeagueTeamId} cannot play itself."),
                nameof(second));
        }

        return Compare(first, second) < 0 ? second.LeagueTeamId : first.LeagueTeamId;
    }
}
