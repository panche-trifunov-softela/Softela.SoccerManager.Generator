namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The one rule that gives a match its date: the first planned date, stepping by whole weeks, that keeps the gaps
/// every match needs. The domestic cup uses it now; the playoffs and other competitions will use it too.
/// </summary>
public static class MatchDateRule
{
    /// <summary>
    /// How many days past the latest date that matters the search goes on before it gives up. With no
    /// <c>before</c> a date always comes within a week of that latest date, so this limit is only a safety stop. It is
    /// the same year that the league allows a first match to be ahead of its run.
    /// </summary>
    private const int MaxDaysAhead = 365;

    /// <summary>
    /// Picks the date of a match. The candidates are the planned date and then the same time of day one week later,
    /// two weeks later and so on. The first candidate that meets all of these wins:
    /// <list type="bullet">
    /// <item><description>is at least 24 hours after the run;</description></item>
    /// <item><description>is after <paramref name="after"/>, when it is given;</description></item>
    /// <item>
    /// <description>is at least 24 hours from every kickoff in <paramref name="clubKickoffs"/>;</description>
    /// </item>
    /// <item>
    /// <description>
    /// is earlier than <paramref name="before"/>, when it is given; reaching it ends the search with no date.
    /// </description>
    /// </item>
    /// </list>
    /// Exactly 24 hours apart is allowed. All dates are UTC. The rule steps by whole weeks only, so every date it
    /// gives falls on the weekday of the planned date; a set of match days will be added when the playoffs need
    /// Tuesday, Thursday and Sunday. With no <paramref name="before"/>, a date always comes within a week after the
    /// latest of the planned date, the earliest moment after the run, <paramref name="after"/> and the end of every
    /// club kickoff's gap, so the year limit is only a safety stop.
    /// </summary>
    /// <param name="planned">The date the calendar plans for the match.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="after">The match must be later than this date, or null when nothing has to come before it.</param>
    /// <param name="before">The match must be earlier than this date, or null when there is no such limit.</param>
    /// <param name="clubKickoffs">The kickoffs of the other matches of both clubs in the season.</param>
    /// <returns>
    /// The date of the match, or null when the candidates reach <paramref name="before"/> first. With no
    /// <paramref name="before"/> it is null only if the year limit stops the search, which is a safety stop.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clubKickoffs"/> is null.</exception>
    public static DateTime? Pick(
        DateTime planned,
        DateTime runTime,
        DateTime? after,
        DateTime? before,
        IReadOnlyCollection<DateTime> clubKickoffs)
    {
        ArgumentNullException.ThrowIfNull(clubKickoffs);

        TimeSpan gap = TimeSpan.FromHours(CalendarTemplate.MinHoursBetweenKickoffs);
        DateTime earliest = runTime + gap;
        DateTime limit = LatestThatMatters(planned, earliest, after, clubKickoffs, gap).AddDays(MaxDaysAhead);

        TimeSpan week = TimeSpan.FromDays(CalendarTemplate.DaysPerWeek);

        for (DateTime candidate = planned; candidate <= limit; candidate += week)
        {
            if (before.HasValue && candidate >= before.Value)
            {
                return null;
            }

            if (candidate < earliest || (after.HasValue && candidate <= after.Value))
            {
                continue;
            }

            if (clubKickoffs.Any(kickoff => (candidate - kickoff).Duration() < gap))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Finds the latest date that can still push a candidate later: the planned date, the earliest date after the run,
    /// the date the match must follow, and the end of the gap after every club kickoff.
    /// </summary>
    /// <param name="planned">The date the calendar plans for the match.</param>
    /// <param name="earliest">The earliest moment the run allows.</param>
    /// <param name="after">The date the match must follow, or null.</param>
    /// <param name="clubKickoffs">The kickoffs of the other matches of both clubs.</param>
    /// <param name="gap">The least time between two matches of a club.</param>
    /// <returns>The latest of those dates.</returns>
    private static DateTime LatestThatMatters(
        DateTime planned,
        DateTime earliest,
        DateTime? after,
        IReadOnlyCollection<DateTime> clubKickoffs,
        TimeSpan gap)
    {
        DateTime latest = planned > earliest ? planned : earliest;

        if (after.HasValue && after.Value > latest)
        {
            latest = after.Value;
        }

        foreach (DateTime kickoff in clubKickoffs)
        {
            if (kickoff + gap > latest)
            {
                latest = kickoff + gap;
            }
        }

        return latest;
    }
}
