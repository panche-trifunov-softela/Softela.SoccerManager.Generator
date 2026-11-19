using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Rebuilds the calendar the league was written on, for the three World Cup steps. Each step logs what it finds in its
/// own words, so this reader only reads.
/// </summary>
internal static class WorldCupCalendarReader
{
    /// <summary>
    /// Rebuilds the calendar from the season's league matches, and counts the clubs of each division.
    /// </summary>
    /// <param name="divisions">The league's divisions, in division order.</param>
    /// <param name="season">The season's matches with their scores.</param>
    /// <returns>
    /// The calendar with the club counts, or no calendar and no club counts when the season has no league match.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the league matches cannot be read, or are not on the matchdays of a calendar.
    /// </exception>
    internal static WorldCupCalendarReading Read(
        IReadOnlyList<GetCompetitionsResult> divisions,
        IReadOnlyList<SeasonMatch> season)
    {
        List<int> divisionIds = divisions.Select(division => division.Id).ToList();
        List<SeasonMatch> leagueMatches = DomesticCupLeagueReader.LeagueMatchesOf(season, divisionIds);

        if (leagueMatches.Count == 0)
        {
            return new WorldCupCalendarReading(null, []);
        }

        Dictionary<int, int> divisionOfClub = DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches);
        List<int> clubCounts = DomesticCupLeagueReader.CountClubsPerDivision(divisionIds, divisionOfClub);
        SeasonCalendar calendar = DomesticCupLeagueReader.RebuildCalendar(clubCounts, leagueMatches);

        return new WorldCupCalendarReading(calendar, clubCounts);
    }
}
