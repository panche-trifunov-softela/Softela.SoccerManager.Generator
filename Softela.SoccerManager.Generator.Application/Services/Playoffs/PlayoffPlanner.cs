using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// Plans the next matches of a division's promotion playoff from what the division's matches say about it. A tie is
/// planned as soon as both of its clubs are known: the semi-finals, or the final of a two-club playoff, on the first
/// Tuesday, Thursday or Sunday after the last league matchday, and the final of a four-club playoff after its
/// semi-finals, planned at 20:00 UTC on the day after the later semi-final. No playoff match is dated at or after the
/// first World Cup finals matchday.
/// </summary>
public static class PlayoffPlanner
{
    /// <summary>
    /// How many days after the last league matchday the playoff is planned to start. The league's last matchday is a
    /// Sunday, so this plans the Tuesday after it.
    /// </summary>
    private const int DaysAfterLastMatchday = 2;

    /// <summary>
    /// Plans the matches to write on this tick, one for each new tie, in tie order. A date comes from
    /// <see cref="MatchDateRule.PickOnDays"/> on the days of <see cref="CalendarTemplate.PlayoffMatchDays"/>: after
    /// the last league matchday, or after the later semi-final for a final that follows them, which is planned at
    /// 20:00 UTC on the day after the later semi-final; before the first World Cup finals matchday, when the calendar
    /// has one; at least 24 hours after the run; and at least 24 hours from every other match of both clubs. Every
    /// planned kickoff is added to both clubs' kickoffs, so the ties planned after it keep their distance.
    /// </summary>
    /// <param name="division">The division, named in a refusal.</param>
    /// <param name="reading">What the division's matches say about its playoff; its status must be ready.</param>
    /// <param name="calendar">The dates of the season.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="clubKickoffs">
    /// The kickoffs of every match of each club in the season, by league team id; the planned matches are added to it.
    /// </param>
    /// <returns>The matches to write, in tie order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="division"/>, <paramref name="reading"/>, <paramref name="calendar"/> or
    /// <paramref name="clubKickoffs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="runTime"/> is not in UTC or the reading is not ready.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the calendar has no league matchday, which is a bug.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when no date fits a tie.</exception>
    public static IReadOnlyList<PlayoffPlannedMatch> Plan(
        GetCompetitionsResult division,
        PlayoffReading reading,
        SeasonCalendar calendar,
        DateTime runTime,
        Dictionary<int, List<DateTime>> clubKickoffs)
    {
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(clubKickoffs);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        if (reading.Status != PlayoffReadingStatus.Ready)
        {
            throw new ArgumentException(
                "The playoff can only be planned once its league is finished and its Standings are settled.",
                nameof(reading));
        }

        if (calendar.LeagueMatchdays.Count == 0)
        {
            throw new InvalidOperationException("The calendar has no league matchday to plan the playoff after.");
        }

        DateTime lastMatchday = calendar.LeagueMatchdays[^1];
        DateTime? worldCupFinals = calendar.WorldCupFinalsMatchdays.Count > 0
            ? calendar.WorldCupFinalsMatchdays[0]
            : null;

        List<PlayoffPlannedMatch> planned = new();

        foreach (PlayoffTie tie in reading.NewTies)
        {
            // The first round is planned for the Tuesday after the last league round, which is a Sunday; the final for
            // the day after the later semi-final at the usual kickoff time. The search then takes the next Tuesday,
            // Thursday or Sunday that keeps every gap.
            bool followsSemiFinals = tie.Stage == PlayoffStages.Final && reading.LatestSemiFinalKickoff is not null;
            DateTime after = followsSemiFinals ? reading.LatestSemiFinalKickoff!.Value : lastMatchday;
            DateTime plannedKickoff = followsSemiFinals
                ? reading.LatestSemiFinalKickoff!.Value.Date.AddDays(1) + CalendarTemplate.KickoffTime.ToTimeSpan()
                : lastMatchday.AddDays(DaysAfterLastMatchday);

            List<DateTime> kickoffs = DomesticCupPlanIndexes.KickoffsOf(
                clubKickoffs,
                tie.HomeLeagueTeamId,
                tie.AwayLeagueTeamId);

            DateTime kickoff = MatchDateRule.PickOnDays(
                plannedKickoff,
                runTime,
                after,
                worldCupFinals,
                kickoffs,
                CalendarTemplate.PlayoffMatchDays)
                ?? throw NoDate(division, tie, plannedKickoff, runTime, after, worldCupFinals);

            DomesticCupPlanIndexes.AddKickoff(clubKickoffs, tie.HomeLeagueTeamId, kickoff);
            DomesticCupPlanIndexes.AddKickoff(clubKickoffs, tie.AwayLeagueTeamId, kickoff);

            planned.Add(new PlayoffPlannedMatch(
                kickoff,
                tie.Stage,
                tie.TieNumber,
                tie.HomeLeagueTeamId,
                tie.AwayLeagueTeamId,
                tie.HomePlace,
                tie.AwayPlace));
        }

        return planned;
    }

    /// <summary>
    /// Builds the refusal for a tie that no date suits. It says which limit is the cause: the clubs' other matches
    /// within a year, a date that could not be found even for a club with no other match, or the clubs' other matches
    /// before the World Cup finals.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="tie">The tie that has no date.</param>
    /// <param name="plannedKickoff">The first date the search tried.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="after">The date the tie must follow.</param>
    /// <param name="worldCupFinals">The first World Cup finals matchday, or null when the calendar has none.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException NoDate(
        GetCompetitionsResult division,
        PlayoffTie tie,
        DateTime plannedKickoff,
        DateTime runTime,
        DateTime after,
        DateTime? worldCupFinals)
    {
        string which = string.Create(
            CultureInfo.InvariantCulture,
            $"{PlayoffStages.NameOf(tie.Stage)} of places {tie.HomePlace} and {tie.AwayPlace} in ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"{PlayoffTableCheck.DivisionOf(division)}, club {tie.HomeLeagueTeamId} v club {tie.AwayLeagueTeamId}");

        const string MoveMatches = "Move the matches of either club that are in the way.";

        if (worldCupFinals is not { } finalsStart)
        {
            return new SeasonRefusedException(
                $"No Tuesday, Thursday or Sunday within a year fits the {which}, at least 24 hours from both clubs' "
                + $"other matches. {MoveMatches}");
        }

        string start = finalsStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        DateTime? withoutClubMatches = MatchDateRule.PickOnDays(
            plannedKickoff,
            runTime,
            after,
            finalsStart,
            [],
            CalendarTemplate.PlayoffMatchDays);

        if (withoutClubMatches is null)
        {
            return new SeasonRefusedException(
                $"The {which}, is too late to be dated before the World Cup finals start on {start}. Insert it by "
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"hand with club {tie.HomeLeagueTeamId} at home; the next tick accepts it."));
        }

        return new SeasonRefusedException(
            $"No Tuesday, Thursday or Sunday before the World Cup finals start on {start} fits the {which}, at least "
            + $"24 hours from both clubs' other matches. {MoveMatches}");
    }
}
