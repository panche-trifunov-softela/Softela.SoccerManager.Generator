using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// Reads one division at the end of a season: its final table and, with a playoff, the playoff's winner.
/// </summary>
internal static class DivisionSeasonEnd
{
    /// <summary>
    /// Reads one division: its final table and, when it has a promotion playoff, the playoff's winner. A division
    /// without a playoff has its Standings structure checked and is ranked from its matches; a division with a playoff
    /// is read by the playoff reader, which also checks the Standings' numbers and waits for them to settle.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the table's tie-break shuffle.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="division">The division.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="divisionLeagueMatches">The division's league matches, all finished.</param>
    /// <param name="season">All the matches of the season.</param>
    /// <param name="standings">The division's Standings rows.</param>
    /// <param name="stadiumOfClub">The stadium id of each club by league team id; null for a club with none.</param>
    /// <returns>The division's table, or what the division waits for.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the division's matches or Standings are refused.
    /// </exception>
    internal static DivisionEnd Read(
        int seasonId,
        DateTime runTime,
        GetCompetitionsResult division,
        PromotionPlaces places,
        IReadOnlyList<SeasonMatch> divisionLeagueMatches,
        IReadOnlyList<SeasonMatch> season,
        IReadOnlyList<GetStandingsResult> standings,
        IReadOnlyDictionary<int, int?> stadiumOfClub)
    {
        if (places.PlayoffClubs == 0)
        {
            PlayoffTableCheck.CheckStructure(division, standings, divisionLeagueMatches, RolloverTexts.OtherFix);

            List<int> table = SeasonTableRanker.RankDivision(
                seasonId,
                PlayoffTableCheck.DivisionOf(division),
                standings,
                divisionLeagueMatches,
                RolloverTexts.RankFix);

            return new DivisionEnd(null, table, null, []);
        }

        List<SeasonMatch> playoffMatches = season
            .Where(match => match.CompetitionId == division.Id && PlayoffStages.IsPlayoffMatch(match))
            .ToList();

        PlayoffReading reading = PlayoffReader.Read(
            seasonId,
            runTime,
            division,
            places,
            divisionLeagueMatches,
            playoffMatches,
            standings,
            stadiumOfClub,
            RolloverTexts.OtherFix,
            RolloverTexts.RankFix);

        return reading.Status switch
        {
            PlayoffReadingStatus.WaitingForLeague => WaitingDivision(
                string.Create(CultureInfo.InvariantCulture, $"the league matches of '{division.Name}'")),
            PlayoffReadingStatus.WaitingForStandings => WaitingDivision(WaitingForStandings(division, reading)),
            PlayoffReadingStatus.Ready => ReadPlayoff(division, reading, playoffMatches),
            _ => throw new InvalidOperationException(
                $"The season end reader has no reason for the status {reading.Status}, which is a bug."),
        };
    }

    /// <summary>
    /// Reads the outcome of a promotion playoff whose league is finished and whose Standings are settled: the final's
    /// winner when the final is decided, and otherwise a wait for the playoff.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="reading">The division's ready playoff reading.</param>
    /// <param name="playoffMatches">The division's playoff matches.</param>
    /// <returns>The division's table and playoff winner, or the wait for the playoff.</returns>
    private static DivisionEnd ReadPlayoff(
        GetCompetitionsResult division,
        PlayoffReading reading,
        IReadOnlyList<SeasonMatch> playoffMatches)
    {
        if (reading.FinalWinnerLeagueTeamId is not { } winner)
        {
            int decided = reading.DecidedMatches;
            int written = reading.WrittenMatches;

            return WaitingDivision(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the promotion playoff of '{division.Name}' ({decided} of {written} matches decided)"));
        }

        return new DivisionEnd(null, reading.RankedLeagueTeamIds, winner, playoffMatches);
    }

    /// <summary>
    /// Describes the wait of a division whose Standings changed less than an hour ago.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="reading">The division's playoff reading, which has the time of the newest change.</param>
    /// <returns>The phrase that completes "it waits for ...".</returns>
    private static string WaitingForStandings(GetCompetitionsResult division, PlayoffReading reading)
    {
        string changedAt = reading.NewestStandingsChange!.Value.ToString(
            StandingsSettleRule.ChangedAtFormat,
            CultureInfo.InvariantCulture);

        return $"the Standings of '{division.Name}', which changed at {changedAt} UTC, to settle for an hour";
    }

    /// <summary>
    /// Builds the reading of a division that the season waits for.
    /// </summary>
    /// <param name="waitingFor">The phrase that completes "it waits for ...".</param>
    /// <returns>The reading, with no table.</returns>
    private static DivisionEnd WaitingDivision(string waitingFor)
    {
        return new DivisionEnd(waitingFor, [], null, []);
    }
}
