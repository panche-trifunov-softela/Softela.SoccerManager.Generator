using System.Globalization;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Dates the matches of a continental cup's league phase. It also dates the groups of the World Cup qualifiers, on
/// their Fridays, in the same way. Each match keeps its matchday's calendar date, a Monday for a cup and a Friday for
/// a qualifier, or moves to the next such weekday that clears both clubs' other matches by 24 hours and comes after
/// the matchday before. The whole league phase must end before the first knockout date, and the qualifiers before the
/// first day of the finals; when it cannot, the season plays without the continental cups or the qualifiers.
/// </summary>
internal static class ContinentalLeaguePhaseDatePlanner
{
    /// <summary>
    /// Tells whether the league phase can still be dated, before any squad is read: each matchday in turn is dated by
    /// <see cref="MatchDateRule.Pick"/> after the matchday before and before the first knockout date, with no club's
    /// matches in the way. It catches a season whose league phase can no longer start in time.
    /// </summary>
    /// <param name="leaguePhaseDates">The calendar date of each matchday, earliest first.</param>
    /// <param name="firstKnockoutDate">
    /// The date of the first knockout match, or of the first day of the finals for the qualifiers; the league phase
    /// ends before it.
    /// </param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>True when every matchday has a date.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leaguePhaseDates"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="runTime"/> is not in UTC.</exception>
    public static bool CanStart(IReadOnlyList<DateTime> leaguePhaseDates, DateTime firstKnockoutDate, DateTime runTime)
    {
        ArgumentNullException.ThrowIfNull(leaguePhaseDates);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        DateTime? previous = null;

        foreach (DateTime planned in leaguePhaseDates)
        {
            previous = MatchDateRule.Pick(planned, runTime, previous, firstKnockoutDate, []);

            if (previous is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Dates the matches of one cup's league phase, or of every World Cup qualifier group. Each match is dated by
    /// <see cref="MatchDateRule.Pick"/> from its matchday's calendar date, after the latest kickoff placed for the
    /// matchday before, before the first knockout date, and at least 24 hours from every other match of both clubs.
    /// Each kickoff is recorded against both clubs, so the matches after it keep their distance. The continental cup
    /// step shares the index across both cups; when the result is null it holds the kickoffs placed before the failure,
    /// and the caller drops it.
    /// </summary>
    /// <param name="fixtures">
    /// The matches of one cup, or of every qualifier group, in draw order: sorted by matchday.
    /// </param>
    /// <param name="leaguePhaseDates">The calendar date of each matchday, earliest first.</param>
    /// <param name="firstKnockoutDate">
    /// The date of the first knockout match, or of the first day of the finals for the qualifiers; the league phase
    /// ends before it.
    /// </param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches; the dated matches are added to it.</param>
    /// <returns>
    /// The kickoff of each match, in the order of <paramref name="fixtures"/>, or null when a match has no date.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument that is a reference type is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="runTime"/> is not in UTC, a match has a matchday without a calendar date, or the
    /// matches are not sorted by matchday.
    /// </exception>
    public static IReadOnlyList<DateTime>? Place(
        IReadOnlyList<ContinentalFixture> fixtures,
        IReadOnlyList<DateTime> leaguePhaseDates,
        DateTime firstKnockoutDate,
        DateTime runTime,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(leaguePhaseDates);
        ArgumentNullException.ThrowIfNull(kickoffsOfClub);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        Dictionary<int, DateTime> latestOfMatchday = new();
        List<DateTime> kickoffs = new();
        int previousMatchday = 0;

        foreach (ContinentalFixture fixture in fixtures)
        {
            if (fixture.Matchday < 1 || fixture.Matchday > leaguePhaseDates.Count)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Matchday {fixture.Matchday} has no calendar date."),
                    nameof(fixtures));
            }

            if (fixture.Matchday < previousMatchday)
            {
                throw new ArgumentException("The matches must be sorted by matchday.", nameof(fixtures));
            }

            previousMatchday = fixture.Matchday;

            DateTime? after = latestOfMatchday.TryGetValue(fixture.Matchday - 1, out DateTime latest) ? latest : null;

            DateTime? kickoff = MatchDateRule.Pick(
                leaguePhaseDates[fixture.Matchday - 1],
                runTime,
                after,
                firstKnockoutDate,
                ClubKickoffs(fixture, kickoffsOfClub));

            if (kickoff is null)
            {
                return null;
            }

            DomesticCupPlanIndexes.AddKickoff(kickoffsOfClub, fixture.HomeLeagueTeamId, kickoff.Value);
            DomesticCupPlanIndexes.AddKickoff(kickoffsOfClub, fixture.AwayLeagueTeamId, kickoff.Value);

            if (!latestOfMatchday.TryGetValue(fixture.Matchday, out DateTime known) || kickoff.Value > known)
            {
                latestOfMatchday[fixture.Matchday] = kickoff.Value;
            }

            kickoffs.Add(kickoff.Value);
        }

        return kickoffs;
    }

    /// <summary>
    /// Gathers the kickoffs of the other matches of both clubs of a match.
    /// </summary>
    /// <param name="fixture">The match to date.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches.</param>
    /// <returns>The kickoffs of the home club's matches and then the away club's.</returns>
    private static List<DateTime> ClubKickoffs(
        ContinentalFixture fixture,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        List<DateTime> clubKickoffs = new();

        foreach (int clubId in new[] { fixture.HomeLeagueTeamId, fixture.AwayLeagueTeamId })
        {
            if (kickoffsOfClub.TryGetValue(clubId, out List<DateTime>? known))
            {
                clubKickoffs.AddRange(known);
            }
        }

        return clubKickoffs;
    }
}
