using System.Globalization;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Works out the days the World Cup finals are played on. The calendar plans them a week after the continental finals
/// it expects, but a continental final that was moved later pushes the finals later by whole days, so the finals are
/// never played before the continental cups are decided. It reads nothing and writes nothing.
/// </summary>
internal static class WorldCupFinalsStart
{
    /// <summary>
    /// Plans the finals days. A continental cup with matches this season but no final yet makes the finals wait for it.
    /// A cup with no match this season is ignored. Otherwise the finals start on the calendar's first finals day, or,
    /// when the latest continental final is later than the calendar expects, so that the first finals day is 7 days
    /// after it; every finals day moves by the same whole number of days and keeps its time of day.
    /// </summary>
    /// <param name="calendarFinalsDays">The 8 finals days the season's calendar plans, earliest first.</param>
    /// <param name="continentalCups">The league's continental cups, in the order of the cup finder.</param>
    /// <param name="season">The season's matches.</param>
    /// <returns>The 8 finals days, or the cup the finals wait for and no days.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the calendar does not have 8 finals days.</exception>
    internal static WorldCupFinalsDays Plan(
        IReadOnlyList<DateTime> calendarFinalsDays,
        IReadOnlyList<GetCompetitionsResult> continentalCups,
        IReadOnlyList<SeasonMatch> season)
    {
        ArgumentNullException.ThrowIfNull(calendarFinalsDays);
        ArgumentNullException.ThrowIfNull(continentalCups);
        ArgumentNullException.ThrowIfNull(season);

        if (calendarFinalsDays.Count != CalendarTemplate.WorldCupFinalsMatchdayCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The calendar has {calendarFinalsDays.Count} finals days, but the finals have ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{CalendarTemplate.WorldCupFinalsMatchdayCount}."),
                nameof(calendarFinalsDays));
        }

        byte continentalFinal = ContinentalKnockoutStages.StageOf(ContinentalRound.Final);
        List<DateTime> finals = [];

        foreach (GetCompetitionsResult cup in continentalCups)
        {
            List<SeasonMatch> cupMatches = season.Where(match => match.CompetitionId == cup.Id).ToList();

            if (cupMatches.Count == 0)
            {
                continue;
            }

            List<SeasonMatch> cupFinals = cupMatches.Where(match => match.Stage == continentalFinal).ToList();

            if (cupFinals.Count == 0)
            {
                return new WorldCupFinalsDays([], cup);
            }

            finals.AddRange(cupFinals.Select(match => match.Kickoff));
        }

        return new WorldCupFinalsDays(ShiftDays(calendarFinalsDays, finals), null);
    }

    /// <summary>
    /// Moves the calendar's finals days later by whole days when a continental final is later than the calendar
    /// expects.
    /// </summary>
    /// <param name="calendarFinalsDays">The finals days the season's calendar plans.</param>
    /// <param name="continentalFinals">The kickoffs of the continental finals; empty when there are none.</param>
    /// <returns>The finals days, moved by the same number of days, which is 0 when no move is needed.</returns>
    private static List<DateTime> ShiftDays(
        IReadOnlyList<DateTime> calendarFinalsDays,
        IReadOnlyList<DateTime> continentalFinals)
    {
        int shift = 0;

        if (continentalFinals.Count > 0)
        {
            DateTime latestFinal = continentalFinals.Max();
            DateTime earliestStart = latestFinal.Date.AddDays(CalendarTemplate.DaysFromLastClubDateToWorldCupFinals);

            shift = Math.Max(0, (earliestStart - calendarFinalsDays[0].Date).Days);
        }

        return calendarFinalsDays.Select(day => day.AddDays(shift)).ToList();
    }
}
