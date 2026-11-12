namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// Rebuilds the calendar a season's league was written on, from the league matches that exist. The steps after the
/// league use it to find the dates of the cups and other competitions, because the calendar itself is not stored.
/// </summary>
public static class SeasonCalendarRebuilder
{
    /// <summary>
    /// Rebuilds the calendar from the divisions' sizes and the kickoffs of the written league matches. The calendar is
    /// built from the day before the earliest kickoff, which gives back the calendar the league was written on: its
    /// first Thursday is that kickoff, and every other date is a fixed distance from it. The rebuild is trusted only
    /// when its league matchdays are exactly the distinct league kickoffs given.
    /// </summary>
    /// <param name="divisionClubCounts">The number of clubs in each division.</param>
    /// <param name="leagueKickoffs">The kickoffs of the league's matches, in UTC and in any order.</param>
    /// <returns>
    /// The calendar, or null when there are no kickoffs or the kickoffs are not the calendar's league matchdays, which
    /// means a league match was moved or made by hand.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="divisionClubCounts"/> or <paramref name="leagueKickoffs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the divisions cannot be laid out as a calendar; see <see cref="SeasonCalendarBuilder.Build"/>.
    /// </exception>
    public static SeasonCalendar? Rebuild(IReadOnlyList<int> divisionClubCounts, IReadOnlyList<DateTime> leagueKickoffs)
    {
        ArgumentNullException.ThrowIfNull(divisionClubCounts);
        ArgumentNullException.ThrowIfNull(leagueKickoffs);

        if (leagueKickoffs.Count == 0)
        {
            return null;
        }

        DateOnly startDate = DateOnly.FromDateTime(leagueKickoffs.Min()).AddDays(-1);
        SeasonCalendar calendar = SeasonCalendarBuilder.Build(startDate, divisionClubCounts, []);

        List<DateTime> distinct = leagueKickoffs.Distinct().Order().ToList();

        if (distinct.Count != calendar.LeagueMatchdays.Count)
        {
            return null;
        }

        for (int index = 0; index < distinct.Count; index++)
        {
            if (distinct[index] != calendar.LeagueMatchdays[index])
            {
                return null;
            }
        }

        return calendar;
    }
}
