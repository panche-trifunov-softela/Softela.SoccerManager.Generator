using System.Collections.Frozen;
using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Dates the matches of the World Cup finals. The finals use every day of the week, so a match keeps its planned day
/// unless the run is too late for it or a nation has another match too close to it; then it moves to the next day that
/// fits. It reads nothing and changes only the kickoffs it is given to record the matches it dates.
/// </summary>
internal static class WorldCupFinalsDates
{
    /// <summary>
    /// All seven weekdays: a finals match may be on any of them.
    /// </summary>
    private static readonly IReadOnlySet<DayOfWeek> EveryWeekday = Enum.GetValues<DayOfWeek>().ToFrozenSet();

    /// <summary>
    /// Dates the group matches. Matchday N is planned on the Nth finals day, and every match of it must be later than
    /// the latest match placed on matchday N - 1, so a late result that moves one matchday moves the later ones too.
    /// Each date is the first day, from the planned one, that the date rule allows with the nations' other matches in
    /// the season, and it is recorded for both nations so the next match sees it.
    /// </summary>
    /// <param name="fixtures">
    /// The matches with the number of their group, sorted by matchday and then by group number.
    /// </param>
    /// <param name="finalsDays">The finals days, earliest first; matchday N is planned on the Nth.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="kickoffsOfClub">
    /// The kickoffs of every nation's matches in the season; the new ones are added.
    /// </param>
    /// <returns>The kickoff of each match, in the order of <paramref name="fixtures"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the matches are not sorted by matchday and then by group number, or a matchday has no finals day.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when no day within a year fits a match.</exception>
    internal static IReadOnlyList<DateTime> PlaceGroupMatches(
        IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> fixtures,
        IReadOnlyList<DateTime> finalsDays,
        DateTime runTime,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(finalsDays);
        ArgumentNullException.ThrowIfNull(kickoffsOfClub);

        CheckFixtures(fixtures, finalsDays);

        Dictionary<int, DateTime> latestOfMatchday = new();
        List<DateTime> kickoffs = [];

        foreach ((byte groupNumber, ContinentalFixture fixture) in fixtures)
        {
            DateTime? after = latestOfMatchday.TryGetValue(fixture.Matchday - 1, out DateTime previous)
                ? previous
                : null;

            string description = string.Create(
                CultureInfo.InvariantCulture,
                $"the matchday {fixture.Matchday} match of group {groupNumber}");

            DateTime kickoff = Place(
                finalsDays[fixture.Matchday - 1],
                runTime,
                after,
                fixture.HomeLeagueTeamId,
                fixture.AwayLeagueTeamId,
                kickoffsOfClub,
                description);

            if (!latestOfMatchday.TryGetValue(fixture.Matchday, out DateTime latest) || kickoff > latest)
            {
                latestOfMatchday[fixture.Matchday] = kickoff;
            }

            kickoffs.Add(kickoff);
        }

        return kickoffs;
    }

    /// <summary>
    /// Dates one knockout match: the first day, from the planned one, that the date rule allows with the nations' other
    /// matches in the season, and later than <paramref name="after"/> when it is given. The date is recorded for both
    /// nations.
    /// </summary>
    /// <param name="planned">The day the match is planned on.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="after">The match must be later than this, or null when nothing has to come before it.</param>
    /// <param name="home">The roster entry of the home nation.</param>
    /// <param name="away">The roster entry of the away nation.</param>
    /// <param name="kickoffsOfClub">The kickoffs of every nation's matches in the season; the new one is added.</param>
    /// <returns>The kickoff of the match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kickoffsOfClub"/> is null.</exception>
    /// <exception cref="SeasonRefusedException">Thrown when no day within a year fits the match.</exception>
    internal static DateTime PickKnockout(
        DateTime planned,
        DateTime runTime,
        DateTime? after,
        int home,
        int away,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        ArgumentNullException.ThrowIfNull(kickoffsOfClub);

        string description = string.Create(
            CultureInfo.InvariantCulture,
            $"the knockout match of league teams {home} and {away}");

        return Place(planned, runTime, after, home, away, kickoffsOfClub, description);
    }

    /// <summary>
    /// Checks that the matches are sorted by matchday and then by group number, and that every matchday has a finals
    /// day.
    /// </summary>
    /// <param name="fixtures">The matches with the number of their group.</param>
    /// <param name="finalsDays">The finals days.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the matches are out of order or a matchday has no day.
    /// </exception>
    private static void CheckFixtures(
        IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> fixtures,
        IReadOnlyList<DateTime> finalsDays)
    {
        for (int index = 0; index < fixtures.Count; index++)
        {
            int matchday = fixtures[index].Fixture.Matchday;

            if (matchday < 1 || matchday > finalsDays.Count)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"A match is on matchday {matchday}, but there are {finalsDays.Count} finals days."),
                    nameof(fixtures));
            }

            if (index == 0)
            {
                continue;
            }

            (byte groupNumber, ContinentalFixture fixture) = fixtures[index - 1];

            if (fixture.Matchday > matchday
                || (fixture.Matchday == matchday && groupNumber > fixtures[index].GroupNumber))
            {
                throw new ArgumentException(
                    "The matches must be sorted by matchday and then by group number.",
                    nameof(fixtures));
            }
        }
    }

    /// <summary>
    /// Finds the date of one match with the date rule, on any weekday, and records it for both nations.
    /// </summary>
    /// <param name="planned">The day the match is planned on.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="after">The match must be later than this, or null.</param>
    /// <param name="home">The roster entry of the home nation.</param>
    /// <param name="away">The roster entry of the away nation.</param>
    /// <param name="kickoffsOfClub">The kickoffs of every nation's matches in the season; the new one is added.</param>
    /// <param name="description">The match as a refusal names it.</param>
    /// <returns>The kickoff of the match.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when no day within a year fits the match.</exception>
    private static DateTime Place(
        DateTime planned,
        DateTime runTime,
        DateTime? after,
        int home,
        int away,
        Dictionary<int, List<DateTime>> kickoffsOfClub,
        string description)
    {
        List<DateTime> nationKickoffs = DomesticCupPlanIndexes.KickoffsOf(kickoffsOfClub, home, away);

        DateTime? kickoff = MatchDateRule.PickOnDays(planned, runTime, after, null, nationKickoffs, EveryWeekday);

        if (kickoff is not { } picked)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"No day within a year fits {description}, a safety stop. Check the season's kickoffs for ")
                + "these nations, or reset the World Cup finals.");
        }

        DomesticCupPlanIndexes.AddKickoff(kickoffsOfClub, home, picked);
        DomesticCupPlanIndexes.AddKickoff(kickoffsOfClub, away, picked);

        return picked;
    }
}
