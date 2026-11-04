namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// Builds the dates of a season on the layout that the shape of its league needs.
/// </summary>
public static class SeasonCalendarBuilder
{
    /// <summary>
    /// Builds the calendar of a season. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// The first league matchday is the earliest date that is after the start date and whose kickoff is at least 48
    /// hours after every existing kickoff; exactly 48 hours is allowed. So the season starts after the latest
    /// existing match, however far ahead that is.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The league has one date per round of a double round robin, and the domestic cup has one per round of a knockout
    /// of all the league's clubs, so how many there are depends on the league's shape. How many continental and World
    /// Cup dates there are does not.
    /// </description>
    /// </item>
    /// <item>
    /// <description>Every date's distance from the first matchday depends only on the league's shape.</description>
    /// </item>
    /// <item>
    /// <description>Every kickoff is at 20:00 UTC.</description>
    /// </item>
    /// <item>
    /// <description>
    /// The result depends only on the start date, the shape and the set of existing kickoffs, so the order in which
    /// rows came back from a database cannot change it, and neither can the clock or the machine's time zone.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="startDate">The date the season is built from; the first matchday is after it.</param>
    /// <param name="divisionClubCounts">
    /// The number of clubs in each division; they must all be the same, even, and at least 10.
    /// </param>
    /// <param name="existingKickoffs">The kickoffs of the matches that already exist, in UTC and in any order.</param>
    /// <returns>The calendar of the season; its start date is <paramref name="startDate"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="divisionClubCounts"/> or <paramref name="existingKickoffs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>there are no divisions;</description></item>
    /// <item><description>the divisions do not all have the same number of clubs;</description></item>
    /// <item><description>a division has fewer than 10 clubs, or an odd number of clubs;</description></item>
    /// <item><description>the league has more than 1024 clubs in all;</description></item>
    /// <item>
    /// <description>
    /// an existing kickoff is not in UTC. Values read from the database are already UTC, so that can only be a bug
    /// upstream.
    /// </description>
    /// </item>
    /// </list>
    /// </exception>
    public static SeasonCalendar Build(
        DateOnly startDate,
        IReadOnlyList<int> divisionClubCounts,
        IReadOnlyList<DateTime> existingKickoffs)
    {
        ArgumentNullException.ThrowIfNull(divisionClubCounts);
        ArgumentNullException.ThrowIfNull(existingKickoffs);

        SeasonCalendarInputValidator.Validate(divisionClubCounts, existingKickoffs);

        int clubsPerDivision = divisionClubCounts[0];

        SeasonLayout layout = SeasonLayoutBuilder.Build(clubsPerDivision, divisionClubCounts.Count);
        DateOnly firstMatchday = FindFirstMatchday(startDate, existingKickoffs);

        List<DateTime> leagueMatchdays = layout.LeagueWeeks
            .Select(week => MatchdayOf(firstMatchday, week))
            .ToList();

        List<DateTime> domesticCupRounds = layout.DomesticCupWeeks
            .Select(week => MidweekOf(firstMatchday, week))
            .ToList();

        List<DateTime> leaguePhase = layout.LeaguePhaseWeeks
            .Select(week => MidweekOf(firstMatchday, week))
            .ToList();

        ContinentalCupDates continentalCups = new(
            leaguePhase,
            LegsOf(firstMatchday, layout.PlayOffWeeks),
            LegsOf(firstMatchday, layout.RoundOf16Weeks),
            LegsOf(firstMatchday, layout.QuarterFinalWeeks),
            LegsOf(firstMatchday, layout.SemiFinalWeeks),
            MidweekOf(firstMatchday, layout.ContinentalFinalWeek));

        List<DateTime> worldCupQualifierMatchdays = layout.WindowWeeks
            .SelectMany(week => new[] { MatchdayOf(firstMatchday, week), MidweekOf(firstMatchday, week) })
            .ToList();

        // The continental final is the last club date, so the World Cup finals are counted from it.
        DateOnly lastClubDate = DateOnly.FromDateTime(continentalCups.Final);
        DateOnly worldCupFinalsStart = lastClubDate.AddDays(CalendarTemplate.DaysFromLastClubDateToWorldCupFinals);

        List<DateTime> worldCupFinalsMatchdays = Enumerable.Range(0, CalendarTemplate.WorldCupFinalsMatchdayCount)
            .Select(index => KickoffAt(
                worldCupFinalsStart,
                index * CalendarTemplate.DaysBetweenWorldCupFinalsMatchdays))
            .ToList();

        return new SeasonCalendar(
            startDate,
            leagueMatchdays,
            domesticCupRounds,
            continentalCups,
            worldCupQualifierMatchdays,
            worldCupFinalsMatchdays);
    }

    /// <summary>
    /// Finds the date of the first league matchday: the earliest date after the start date whose kickoff is at least
    /// <see cref="CalendarTemplate.MinHoursBetweenKickoffs"/> hours after every existing kickoff.
    /// </summary>
    /// <param name="startDate">The date the season is built from; the first matchday is after it.</param>
    /// <param name="existingKickoffs">The kickoffs of the matches that already exist, in UTC and in any order.</param>
    /// <returns>The date of game week 1's day 0.</returns>
    private static DateOnly FindFirstMatchday(DateOnly startDate, IReadOnlyList<DateTime> existingKickoffs)
    {
        DateOnly firstMatchday = startDate.AddDays(1);

        foreach (DateTime kickoff in existingKickoffs)
        {
            DateOnly earliest = EarliestMatchdayFor(kickoff);

            if (earliest > firstMatchday)
            {
                firstMatchday = earliest;
            }
        }

        return firstMatchday;
    }

    /// <summary>
    /// Finds the earliest matchday whose kickoff is at least <see cref="CalendarTemplate.MinHoursBetweenKickoffs"/>
    /// hours after one existing kickoff: the day that gap ends on, or the next day when the gap ends after the kickoff
    /// time.
    /// </summary>
    /// <param name="existingKickoff">The kickoff of an existing match, in UTC.</param>
    /// <returns>The date of the earliest matchday that keeps the gap.</returns>
    private static DateOnly EarliestMatchdayFor(DateTime existingKickoff)
    {
        DateTime earliestKickoff = existingKickoff.AddHours(CalendarTemplate.MinHoursBetweenKickoffs);
        DateOnly date = DateOnly.FromDateTime(earliestKickoff);

        // Exactly the gap is allowed, so a time of day after the kickoff time, even by one tick, needs the next day.
        return TimeOnly.FromDateTime(earliestKickoff) > CalendarTemplate.KickoffTime ? date.AddDays(1) : date;
    }

    /// <summary>
    /// Gives the kickoff on day 0 of a game week: the league matchday, or the first qualifier matchday of an
    /// international window.
    /// </summary>
    /// <param name="firstMatchday">The date of game week 1's day 0.</param>
    /// <param name="gameWeek">The 1-based game week.</param>
    /// <returns>The kickoff on the game week's day 0, in UTC.</returns>
    private static DateTime MatchdayOf(DateOnly firstMatchday, int gameWeek)
    {
        return KickoffAt(firstMatchday, (gameWeek - 1) * CalendarTemplate.DaysPerGameWeek);
    }

    /// <summary>
    /// Gives the midweek slot of a game week.
    /// </summary>
    /// <param name="firstMatchday">The date of game week 1's day 0.</param>
    /// <param name="gameWeek">The 1-based game week.</param>
    /// <returns>The kickoff in the game week's midweek slot, in UTC.</returns>
    private static DateTime MidweekOf(DateOnly firstMatchday, int gameWeek)
    {
        return KickoffAt(
            firstMatchday,
            (gameWeek - 1) * CalendarTemplate.DaysPerGameWeek + CalendarTemplate.MidweekDayOffset);
    }

    /// <summary>
    /// Gives the kickoff on the day a number of days after a date, at the template's kickoff time.
    /// </summary>
    /// <param name="date">The date to count from.</param>
    /// <param name="dayOffset">The number of days after the date; 0 is the date itself.</param>
    /// <returns>The kickoff, with the kind set to UTC.</returns>
    private static DateTime KickoffAt(DateOnly date, int dayOffset)
    {
        return date.AddDays(dayOffset).ToDateTime(CalendarTemplate.KickoffTime, DateTimeKind.Utc);
    }

    /// <summary>
    /// Gives the kickoffs of the two legs of a tie, both in the midweek slot of their game weeks.
    /// </summary>
    /// <param name="firstMatchday">The date of game week 1's day 0.</param>
    /// <param name="weeks">The game weeks of the first leg and of the second leg.</param>
    /// <returns>The kickoffs of the two legs, in UTC.</returns>
    private static TwoLegDates LegsOf(DateOnly firstMatchday, (int FirstLeg, int SecondLeg) weeks)
    {
        return new TwoLegDates(
            MidweekOf(firstMatchday, weeks.FirstLeg),
            MidweekOf(firstMatchday, weeks.SecondLeg));
    }
}
