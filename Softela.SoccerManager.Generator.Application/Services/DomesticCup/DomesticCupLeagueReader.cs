using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Reads the league side of a season for the cup: which division each club plays in, how big the divisions are and
/// which calendar the league was written on.
/// </summary>
internal static class DomesticCupLeagueReader
{
    /// <summary>
    /// Finds the division of every club that plays a league match.
    /// </summary>
    /// <param name="leagueMatches">The league matches, in kickoff order.</param>
    /// <returns>The competition id of the division, by league team id.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a league match lacks a club, or a club plays in two divisions.
    /// </exception>
    public static Dictionary<int, int> MapClubsToDivisions(List<SeasonMatch> leagueMatches)
    {
        Dictionary<int, int> divisionOfClub = new();

        foreach (SeasonMatch match in leagueMatches)
        {
            if (match.HomeLeagueTeamId is not { } home || match.AwayLeagueTeamId is not { } away)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"League match {match.Id} has no home or no away club, so the cup cannot read who plays in ")
                    + "the league. Fix its statistic rows, or reset the season.");
            }

            int division = match.CompetitionId!.Value;

            foreach (int club in new[] { home, away })
            {
                if (divisionOfClub.TryAdd(club, division) || divisionOfClub[club] == division)
                {
                    continue;
                }

                int other = divisionOfClub[club];

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {club} plays league matches in two divisions, competitions {Math.Min(division, other)} ")
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"and {Math.Max(division, other)}. Fix the league matches, or reset the season."));
            }
        }

        return divisionOfClub;
    }

    /// <summary>
    /// Counts the clubs of each division.
    /// </summary>
    /// <param name="divisionIdsInOrder">The competition ids of the league's divisions, top division first.</param>
    /// <param name="divisionOfClub">The competition id of the division, by league team id.</param>
    /// <returns>The number of clubs in each division, in division order.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a division has no clubs.</exception>
    public static List<int> CountClubsPerDivision(
        IReadOnlyList<int> divisionIdsInOrder,
        Dictionary<int, int> divisionOfClub)
    {
        List<int> counts = new();

        foreach (int divisionId in divisionIdsInOrder)
        {
            int count = divisionOfClub.Values.Count(division => division == divisionId);

            if (count == 0)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Division {divisionId} has no league matches while the other divisions have them, and ")
                    + "the cup needs the whole league. Reset the season so its league is written again.");
            }

            counts.Add(count);
        }

        return counts;
    }

    /// <summary>
    /// Rebuilds the calendar the league was written on.
    /// </summary>
    /// <param name="clubCounts">The number of clubs in each division, in division order.</param>
    /// <param name="leagueMatches">The league matches.</param>
    /// <returns>The calendar.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the divisions cannot be laid out, or the league matches are not on the calendar's matchdays.
    /// </exception>
    public static SeasonCalendar RebuildCalendar(List<int> clubCounts, List<SeasonMatch> leagueMatches)
    {
        List<DateTime> kickoffs = leagueMatches.Select(match => match.Kickoff).ToList();
        SeasonCalendar? calendar;

        try
        {
            calendar = SeasonCalendarRebuilder.Rebuild(clubCounts, kickoffs);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            string reason = DomesticCupReader.ReasonOf(exception);

            throw new SeasonRefusedException(
                $"The league matches give divisions the cup's calendar cannot use: {reason}");
        }

        return calendar ?? throw new SeasonRefusedException(
            "The league matches are not on the matchdays of their season's calendar: one was moved or made by hand, "
            + "so the cup's dates cannot be rebuilt. Move it back, or reset the season.");
    }
}
