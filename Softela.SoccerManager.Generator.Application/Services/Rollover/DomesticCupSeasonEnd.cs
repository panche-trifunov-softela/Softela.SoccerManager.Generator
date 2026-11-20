using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// Reads the domestic cup at the end of a season: the cup's final holds the season until it is finished, and a season
/// played without a cup is not held.
/// </summary>
internal static class DomesticCupSeasonEnd
{
    /// <summary>
    /// Reads the domestic cup of the season: it has none, it is not played, it is decided, or it is not finished.
    /// </summary>
    /// <param name="seasonId">The id of the season.</param>
    /// <param name="divisionIds">The competition ids of the league's divisions, top division first.</param>
    /// <param name="competitions">The league's competitions.</param>
    /// <param name="season">All the matches of the season.</param>
    /// <returns>
    /// What the season waits for, or the matches that decided the cup; no matches when the season is played without a
    /// cup.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when two competitions have the cup's shape, or the cup's matches are refused.
    /// </exception>
    internal static DomesticCupEnd Read(
        int seasonId,
        IReadOnlyList<int> divisionIds,
        IReadOnlyList<GetCompetitionsResult> competitions,
        IReadOnlyList<SeasonMatch> season)
    {
        IReadOnlyList<GetCompetitionsResult> cups = DomesticCupFinder.Find(competitions);

        if (cups.Count == 0)
        {
            return new DomesticCupEnd(null, []);
        }

        if (cups.Count > 1)
        {
            string described = string.Join(
                ", ",
                cups.Select(cup => string.Create(CultureInfo.InvariantCulture, $"'{cup.Name}' (id {cup.Id})")));

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{cups.Count} competitions have the domestic cup's shape ({described}), so the domestic cup ")
                + "step refuses the cup and the season has no cup final. Keep that shape on one of them.");
        }

        GetCompetitionsResult cup = cups[0];

        DomesticCupReading reading = DomesticCupReader.Read(
            seasonId,
            cup.Id,
            divisionIds,
            season,
            RolloverTexts.OtherFix);

        if (reading.Status == DomesticCupReadingStatus.WaitingForLeague)
        {
            return new DomesticCupEnd(RolloverTexts.LeagueMatchesNotWritten, []);
        }

        if (reading.Status == DomesticCupReadingStatus.NotStarted)
        {
            return new DomesticCupEnd(null, []);
        }

        if (reading.Progress!.ChampionLeagueTeamId is null)
        {
            return new DomesticCupEnd(
                string.Create(CultureInfo.InvariantCulture, $"the domestic cup final of '{cup.Name}'"),
                []);
        }

        return new DomesticCupEnd(null, season.Where(match => match.CompetitionId == cup.Id).ToList());
    }
}
