using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// Reads whether a league's season has ended and, when it has, how its divisions finished. A season ends by its shape,
/// with no date involved: every league match, every promotion playoff match of the divisions that have a playoff and
/// the domestic cup's final are finished (a season with no cup match is played without a cup); and, from 12 clubs per
/// division, also the World Cup final when the season plays a World Cup, or else the finals of its continental cups.
/// Friendlies never hold a season. A division with a playoff is read as the playoff step reads it, so its Standings
/// must agree with its matches and settle for an hour; the other divisions are ranked from their matches, and their
/// Standings numbers are not read. See the readme's "Season rollover".
/// </summary>
internal static class SeasonEndReader
{
    /// <summary>
    /// Reads whether the season has ended. The checks run in this order, and the first wait or refusal found ends the
    /// reading, so a refusal is never hidden by a later wait:
    /// <list type="number">
    /// <item><description>the season has league matches, or it waits for them;</description></item>
    /// <item>
    /// <description>
    /// every league match has both clubs, no club plays in two divisions and no division is empty;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// the Standings: some division has rows, every division has rows, and no row is for a guest of the roster;
    /// </description>
    /// </item>
    /// <item><description>the divisions' settings add up;</description></item>
    /// <item><description>every league match is finished, or the season waits for it;</description></item>
    /// <item>
    /// <description>
    /// every division is read, top division first: its table, and for a playoff division the playoff's winner;
    /// </description>
    /// </item>
    /// <item><description>the domestic cup's final is finished, when the cup has matches;</description></item>
    /// <item><description>from 12 clubs per division, the international finals are finished.</description></item>
    /// </list>
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the tables' tie-break shuffle.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="divisionsInOrder">The league's divisions, the top division first.</param>
    /// <param name="competitions">The league's competitions, divisions included.</param>
    /// <param name="season">All the matches of the season with their scores, in any order.</param>
    /// <param name="standingsByDivision">
    /// The season's Standings rows by division competition id; every division has an entry, possibly empty.
    /// </param>
    /// <param name="roster">The league's roster, which tells the guests.</param>
    /// <param name="stadiumOfClub">The stadium id of each club by league team id; null for a club with none.</param>
    /// <returns>The reading: running with the first thing it waits for, or ended with the divisions' tables.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a division has no entry in <paramref name="standingsByDivision"/>, or a helper refuses its input.
    /// That is a bug in the caller, not a refusal.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the season cannot be read: a league match lacks a club, a club is in two divisions, the Standings do
    /// not fit the matches or the roster, the settings are refused, a playoff, cup or final is not as it should be, or
    /// the league has two domestic cups or two national competitions.
    /// </exception>
    internal static SeasonEndReading Read(
        int seasonId,
        DateTime runTime,
        IReadOnlyList<GetCompetitionsResult> divisionsInOrder,
        IReadOnlyList<GetCompetitionsResult> competitions,
        IReadOnlyList<SeasonMatch> season,
        IReadOnlyDictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision,
        IReadOnlyList<GetLeagueTeamsResult> roster,
        IReadOnlyDictionary<int, int?> stadiumOfClub)
    {
        ArgumentNullException.ThrowIfNull(divisionsInOrder);
        ArgumentNullException.ThrowIfNull(competitions);
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(standingsByDivision);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(stadiumOfClub);

        List<int> divisionIds = divisionsInOrder.Select(division => division.Id).ToList();
        List<SeasonMatch> leagueMatches = DomesticCupLeagueReader.LeagueMatchesOf(season, divisionIds);
        IReadOnlyList<GetCompetitionsResult> formerDivisions = FormerDivisionsOf(competitions, season);

        if (leagueMatches.Count == 0)
        {
            return Running(RolloverTexts.LeagueMatchesNotWritten, formerDivisions);
        }

        Dictionary<int, int> divisionOfClub =
            DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches, RolloverTexts.OtherFix);

        List<int> clubCounts = DomesticCupLeagueReader.CountClubsPerDivision(
            divisionIds,
            divisionOfClub,
            RolloverTexts.MissingDivisionFix);

        RolloverStandings.Check(divisionsInOrder, standingsByDivision, roster);

        IReadOnlyList<PromotionPlaces> places = PromotionSettings.Check(divisionsInOrder, clubCounts);
        int finished = leagueMatches.Count(match => match.IsFinished);

        if (finished < leagueMatches.Count)
        {
            return Running(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{finished} of {leagueMatches.Count} league matches to be finished"),
                formerDivisions);
        }

        List<SeasonMatch> deciding = new(leagueMatches);
        List<IReadOnlyList<int>> tables = [];
        List<int?> playoffWinners = [];

        for (int index = 0; index < divisionsInOrder.Count; index++)
        {
            GetCompetitionsResult division = divisionsInOrder[index];

            DivisionEnd end = DivisionSeasonEnd.Read(
                seasonId,
                runTime,
                division,
                places[index],
                leagueMatches.Where(match => match.CompetitionId == division.Id).ToList(),
                season,
                RolloverStandings.Of(standingsByDivision, division),
                stadiumOfClub);

            if (end.WaitingFor is not null)
            {
                return Running(end.WaitingFor, formerDivisions);
            }

            tables.Add(end.Table);
            playoffWinners.Add(end.PlayoffWinner);
            deciding.AddRange(end.PlayoffMatches);
        }

        DomesticCupEnd cup = DomesticCupSeasonEnd.Read(seasonId, divisionIds, competitions, season);

        if (cup.WaitingFor is not null)
        {
            return Running(cup.WaitingFor, formerDivisions);
        }

        deciding.AddRange(cup.DecidingMatches);

        if (CalendarTemplate.HasInternationalCompetitions(clubCounts[0]))
        {
            InternationalEnd international = InternationalSeasonEnd.Read(competitions, season);

            if (international.WaitingFor is not null)
            {
                return Running(international.WaitingFor, formerDivisions);
            }

            deciding.AddRange(international.DecidingMatches);
        }

        return new SeasonEndReading(
            SeasonEndStatus.Ended,
            null,
            places,
            tables,
            playoffWinners,
            deciding.Max(match => match.Kickoff),
            formerDivisions);
    }

    /// <summary>
    /// Builds the reading of a season that is still running.
    /// </summary>
    /// <param name="waitingFor">The phrase that completes "it waits for ...".</param>
    /// <param name="formerDivisions">The competitions that were divisions of the season and are not any more.</param>
    /// <returns>The reading, with no places, tables, playoff winners or deciding kickoff.</returns>
    private static SeasonEndReading Running(string waitingFor, IReadOnlyList<GetCompetitionsResult> formerDivisions)
    {
        return new SeasonEndReading(SeasonEndStatus.Running, waitingFor, [], [], [], null, formerDivisions);
    }

    /// <summary>
    /// Finds the competitions that have the shape of a division (domestic, not national, group-only, no age limit) but
    /// Order 0, and have a league match in the season: tiers that were given Order 0 after the season was written.
    /// </summary>
    /// <param name="competitions">The league's competitions.</param>
    /// <param name="season">All the matches of the season.</param>
    /// <returns>The competitions, by competition id.</returns>
    private static List<GetCompetitionsResult> FormerDivisionsOf(
        IReadOnlyList<GetCompetitionsResult> competitions,
        IReadOnlyList<SeasonMatch> season)
    {
        return competitions
            .Where(competition => competition.Order == 0
                && competition.IsDomestic
                && !competition.IsNational
                && competition.Format == LeagueInputBuilder.GroupOnlyFormat
                && competition.MaxAgeAllowed is null)
            .Where(competition => season.Any(match => match.Stage is null && match.CompetitionId == competition.Id))
            .OrderBy(competition => competition.Id)
            .ToList();
    }
}
