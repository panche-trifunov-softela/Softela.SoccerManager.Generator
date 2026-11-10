namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// Builds the dates of a season on the layout that the shape of its league needs. Only a league of 12 clubs per
/// division or more has continental cups and a World Cup.
/// </summary>
public static class SeasonCalendarBuilder
{
    /// <summary>
    /// Builds the calendar of a season. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// The first match is on the earliest Thursday that is after the start date and whose kickoff is at least 24 hours
    /// after every existing kickoff; exactly 24 hours is allowed. So the season starts after the latest existing match,
    /// however far ahead that is.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// League rounds are on Thursdays and Sundays and domestic cup rounds on Tuesdays. From 12 clubs per division,
    /// continental dates are on Mondays and World Cup qualifier matchdays on Fridays; the continental final is the last
    /// club date, and the World Cup finals matchdays are on consecutive days that start 7 days after it. Below 12 clubs
    /// there are no continental, qualifier or World Cup finals dates.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The league has one date per round of a double round robin, and the domestic cup has one per round of a knockout
    /// of all the league's clubs, so how many there are depends on the league's shape. So do the continental dates: a
    /// division of 18 clubs or more plays 32-club cups, with 17 dates including a play-off and a round of 16, and a
    /// division of 12 to 16 clubs plays 16-club cups, with 11 dates and neither of those. The World Cup dates are the
    /// same in every league that has them.
    /// </description>
    /// </item>
    /// <item>
    /// <description>Every date's distance from the first Thursday depends only on the league's shape.</description>
    /// </item>
    /// <item>
    /// <description>Every kickoff is at 20:00 UTC, and every weekday is a UTC weekday.</description>
    /// </item>
    /// <item>
    /// <description>
    /// The result depends only on the start date, the shape and the set of existing kickoffs, so the order in which
    /// rows came back from a database cannot change it, and neither can the clock or the machine's time zone.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="startDate">The date the season is built from; the first match is after it.</param>
    /// <param name="divisionClubCounts">
    /// The number of clubs in each division. There can be at most 5 divisions, and the numbers must all be the same,
    /// even, at least 10 and at most 20.
    /// </param>
    /// <param name="existingKickoffs">The kickoffs of the matches that already exist, in UTC and in any order.</param>
    /// <returns>The calendar of the season; its start date is <paramref name="startDate"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="divisionClubCounts"/> or <paramref name="existingKickoffs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>there are no divisions, or more than 5;</description></item>
    /// <item><description>the divisions do not all have the same number of clubs;</description></item>
    /// <item>
    /// <description>a division has fewer than 10 clubs, more than 20 clubs, or an odd number of clubs;</description>
    /// </item>
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
        DateOnly firstThursday = FindFirstThursday(startDate, existingKickoffs);

        List<DateTime> leagueMatchdays = Enumerable.Range(CalendarTemplate.FirstWeek, layout.LeagueWeekCount)
            .SelectMany(week => new[]
            {
                KickoffOf(firstThursday, week, CalendarTemplate.ThursdayOffset),
                KickoffOf(firstThursday, week, CalendarTemplate.SundayOffset),
            })
            .ToList();

        List<DateTime> domesticCupRounds = layout.DomesticCupWeeks
            .Select(week => KickoffOf(firstThursday, week, CalendarTemplate.TuesdayOffset))
            .ToList();

        if (layout.Continental is not { } continental)
        {
            return new SeasonCalendar(startDate, leagueMatchdays, domesticCupRounds, null, [], []);
        }

        List<DateTime> leaguePhase = continental.LeaguePhaseWeeks
            .Select(week => KickoffOf(firstThursday, week, CalendarTemplate.MondayOffset))
            .ToList();

        ContinentalCupDates continentalCups = new(
            leaguePhase,
            continental.PlayOffWeeks is { } playOffWeeks ? LegsOf(firstThursday, playOffWeeks) : null,
            continental.RoundOf16Weeks is { } roundOf16Weeks ? LegsOf(firstThursday, roundOf16Weeks) : null,
            LegsOf(firstThursday, continental.QuarterFinalWeeks),
            LegsOf(firstThursday, continental.SemiFinalWeeks),
            KickoffOf(firstThursday, continental.FinalWeek, CalendarTemplate.MondayOffset));

        List<DateTime> worldCupQualifierMatchdays = layout.QualifierWeeks
            .Select(week => KickoffOf(firstThursday, week, CalendarTemplate.FridayOffset))
            .ToList();

        // Only a league with continental cups gets here. Its continental final is the last club date, so the World Cup
        // finals are counted from it.
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
    /// Finds the first Thursday of the season: the earliest Thursday after the start date whose kickoff is at least
    /// <see cref="CalendarTemplate.MinHoursBetweenKickoffs"/> hours after every existing kickoff.
    /// </summary>
    /// <param name="startDate">The date the season is built from; the first Thursday is after it.</param>
    /// <param name="existingKickoffs">The kickoffs of the matches that already exist, in UTC and in any order.</param>
    /// <returns>The date of the Thursday that starts week 1.</returns>
    private static DateOnly FindFirstThursday(DateOnly startDate, IReadOnlyList<DateTime> existingKickoffs)
    {
        DateOnly earliestDate = startDate.AddDays(1);

        foreach (DateTime kickoff in existingKickoffs)
        {
            DateOnly earliest = EarliestMatchdayFor(kickoff);

            if (earliest > earliestDate)
            {
                earliestDate = earliest;
            }
        }

        int daysToThursday = ((int)DayOfWeek.Thursday - (int)earliestDate.DayOfWeek + CalendarTemplate.DaysPerWeek)
            % CalendarTemplate.DaysPerWeek;

        return earliestDate.AddDays(daysToThursday);
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
    /// Gives the kickoff on a day of a week.
    /// </summary>
    /// <param name="firstThursday">The date of the Thursday that starts week 1.</param>
    /// <param name="week">The 1-based week.</param>
    /// <param name="dayOffset">The number of days after the week's Thursday.</param>
    /// <returns>The kickoff on that day, in UTC.</returns>
    private static DateTime KickoffOf(DateOnly firstThursday, int week, int dayOffset)
    {
        return KickoffAt(firstThursday, (week - CalendarTemplate.FirstWeek) * CalendarTemplate.DaysPerWeek + dayOffset);
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
    /// Gives the kickoffs of the two legs of a tie, both on the Monday of their weeks.
    /// </summary>
    /// <param name="firstThursday">The date of the Thursday that starts week 1.</param>
    /// <param name="weeks">The weeks of the first leg and of the second leg.</param>
    /// <returns>The kickoffs of the two legs, in UTC.</returns>
    private static TwoLegDates LegsOf(DateOnly firstThursday, (int FirstLeg, int SecondLeg) weeks)
    {
        return new TwoLegDates(
            KickoffOf(firstThursday, weeks.FirstLeg, CalendarTemplate.MondayOffset),
            KickoffOf(firstThursday, weeks.SecondLeg, CalendarTemplate.MondayOffset));
    }
}
