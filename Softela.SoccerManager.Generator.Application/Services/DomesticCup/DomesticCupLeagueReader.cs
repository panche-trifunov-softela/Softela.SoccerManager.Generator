using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Reads the league side of a season for the cups and the playoffs: which division each club plays in, how big the
/// divisions are and which calendar the league was written on.
/// </summary>
internal static class DomesticCupLeagueReader
{
    /// <summary>
    /// The fix that a refusal about the league's matches or dates offers besides fixing them by hand, after the word
    /// "or". It resets a season that is still in play, whose league is then written again.
    /// </summary>
    internal const string DefaultOtherFix = "reset the season";

    /// <summary>
    /// The fix, as a sentence, that the refusal of a division with no league matches ends with. It resets a season that
    /// is still in play, whose league is then written again.
    /// </summary>
    internal const string DefaultMissingDivisionFix = "Reset the season so its league is written again.";

    /// <summary>
    /// Picks the league matches out of the season: the matches with no stage that belong to a division of the league,
    /// in kickoff order.
    /// </summary>
    /// <param name="season">The season's matches with their scores.</param>
    /// <param name="divisionIds">The competition ids of the league's divisions.</param>
    /// <returns>The league matches, in kickoff order and then id order.</returns>
    public static List<SeasonMatch> LeagueMatchesOf(IReadOnlyList<SeasonMatch> season, IReadOnlyList<int> divisionIds)
    {
        HashSet<int> divisionSet = new(divisionIds);

        return season
            .Where(match => match.Stage is null && match.CompetitionId is { } id && divisionSet.Contains(id))
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();
    }

    /// <summary>
    /// Finds the division of every club that plays a league match.
    /// </summary>
    /// <param name="leagueMatches">The league matches, in kickoff order.</param>
    /// <param name="otherFix">
    /// The fix that a refusal offers besides fixing the matches or the statistic rows by hand, after the word "or".
    /// It defaults to resetting the season.
    /// </param>
    /// <returns>The competition id of the division, by league team id.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a league match lacks a club, or a club plays in two divisions.
    /// </exception>
    public static Dictionary<int, int> MapClubsToDivisions(
        List<SeasonMatch> leagueMatches,
        string otherFix = DefaultOtherFix)
    {
        Dictionary<int, int> divisionOfClub = new();

        foreach (SeasonMatch match in leagueMatches)
        {
            if (match.HomeLeagueTeamId is not { } home || match.AwayLeagueTeamId is not { } away)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"League match {match.Id} has no home or no away club, so the generator cannot read who plays ")
                    + $"in the league. Fix its statistic rows, or {otherFix}.");
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
                        $"and {Math.Max(division, other)}. Fix the league matches, or ")
                    + $"{otherFix}.");
            }
        }

        return divisionOfClub;
    }

    /// <summary>
    /// Counts the clubs of each division.
    /// </summary>
    /// <param name="divisionIdsInOrder">The competition ids of the league's divisions, top division first.</param>
    /// <param name="divisionOfClub">The competition id of the division, by league team id.</param>
    /// <param name="fix">
    /// The fix, as a sentence, that the refusal of a division with no clubs ends with. It defaults to resetting the
    /// season.
    /// </param>
    /// <returns>The number of clubs in each division, in division order.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a division has no clubs.</exception>
    public static List<int> CountClubsPerDivision(
        IReadOnlyList<int> divisionIdsInOrder,
        Dictionary<int, int> divisionOfClub,
        string fix = DefaultMissingDivisionFix)
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
                    + "the cups and the playoffs need the whole league. "
                    + fix);
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
    /// <param name="otherFix">
    /// The fix that the refusal of matches off the calendar offers besides moving them back by hand, after the word
    /// "or". It defaults to resetting the season.
    /// </param>
    /// <returns>The calendar.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the divisions cannot be laid out, or the league matches are not on the calendar's matchdays.
    /// </exception>
    public static SeasonCalendar RebuildCalendar(
        List<int> clubCounts,
        List<SeasonMatch> leagueMatches,
        string otherFix = DefaultOtherFix)
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
                $"The league matches give divisions the season's calendar cannot use: {reason}");
        }

        return calendar ?? throw new SeasonRefusedException(
            "The league matches are not on the matchdays of their season's calendar: one was moved or made by hand, "
            + $"so the season's dates cannot be rebuilt. Move it back, or {otherFix}.");
    }
}
