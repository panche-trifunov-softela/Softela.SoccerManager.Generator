using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// Checks that a division's Standings agree with its league matches before the playoff is drawn from them. The
/// playoff's places come from the matches, so a Standings row that disagrees with them shows that a person has changed
/// one side by hand and has not yet changed the other.
/// </summary>
public static class PlayoffTableCheck
{
    /// <summary>
    /// The fix that undoes the league's matches and Standings, as the readme describes it.
    /// </summary>
    private const string Reset = "reset the season (the readme's 'Writing a season again')";

    /// <summary>
    /// Checks the Standings of a division against its league matches. The checks run in this order, and the first one
    /// that fails refuses; clubs are taken in league team id order, so the same fault always gives the same message:
    /// <list type="number">
    /// <item>
    /// <description>
    /// the division has as many league matches as its Standings clubs need to meet each other home and away;
    /// </description>
    /// </item>
    /// <item><description>every club of a league match has a Standings row;</description></item>
    /// <item><description>every ordered pair of Standings clubs met exactly once;</description></item>
    /// <item>
    /// <description>
    /// each club's wins, draws and losses add up to its league matches, so the Standings leave playoff matches out;
    /// </description>
    /// </item>
    /// <item>
    /// <description>each club's goals for and against equal the sums over its league matches.</description>
    /// </item>
    /// </list>
    /// A duplicated Standings row fails one of these checks.
    /// </summary>
    /// <param name="division">The division, named in a refusal.</param>
    /// <param name="standings">The division's Standings rows.</param>
    /// <param name="leagueMatches">The division's league matches, all finished and with both clubs.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="division"/>, <paramref name="standings"/> or <paramref name="leagueMatches"/> is
    /// null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a league match lacks a club, which the caller was to have checked.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when the Standings disagree with the league matches.</exception>
    public static void Check(
        GetCompetitionsResult division,
        IReadOnlyList<GetStandingsResult> standings,
        IReadOnlyList<SeasonMatch> leagueMatches)
    {
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(leagueMatches);

        Dictionary<int, GetStandingsResult> rowOfClub = new();

        foreach (GetStandingsResult row in standings)
        {
            rowOfClub.TryAdd(row.LeagueTeamId, row);
        }

        List<int> clubs = rowOfClub.Keys.Order().ToList();

        CheckMatchCount(division, standings.Count, leagueMatches.Count);
        CheckEveryClubHasRow(division, rowOfClub, leagueMatches);
        CheckEveryPairMetOnce(division, clubs, leagueMatches);

        foreach (int clubId in clubs)
        {
            CheckMatchesCounted(division, rowOfClub[clubId], standings.Count);
        }

        foreach (int clubId in clubs)
        {
            CheckGoals(division, rowOfClub[clubId], leagueMatches);
        }
    }

    /// <summary>
    /// Writes a division as a refusal message names it, in lower case: <c>division 'Name' (id 5)</c>.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <returns>The division's name and id.</returns>
    internal static string DivisionOf(GetCompetitionsResult division)
    {
        return string.Create(CultureInfo.InvariantCulture, $"division '{division.Name}' (id {division.Id})");
    }

    /// <summary>
    /// Checks that the division has the number of league matches its Standings clubs play: every club hosts every
    /// other club once.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="clubCount">The number of Standings rows.</param>
    /// <param name="matchCount">The number of league matches.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the numbers do not agree.</exception>
    private static void CheckMatchCount(GetCompetitionsResult division, int clubCount, int matchCount)
    {
        long expected = checked((long)clubCount * (clubCount - 1));

        if (matchCount == expected)
        {
            return;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Division '{division.Name}' (id {division.Id}) has {matchCount} league matches, but the ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"{clubCount} clubs of its Standings play {expected}: a Standings row is missing or extra, or league ")
            + $"matches were added or deleted by hand. Fix the Standings, or {Reset}.");
    }

    /// <summary>
    /// Checks that every club that plays a league match has a Standings row.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="rowOfClub">The Standings row of each club by league team id.</param>
    /// <param name="leagueMatches">The league matches.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a club has no row.</exception>
    private static void CheckEveryClubHasRow(
        GetCompetitionsResult division,
        Dictionary<int, GetStandingsResult> rowOfClub,
        IReadOnlyList<SeasonMatch> leagueMatches)
    {
        int? missing = leagueMatches
            .SelectMany(match => new[] { match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value })
            .Where(clubId => !rowOfClub.ContainsKey(clubId))
            .Select(clubId => (int?)clubId)
            .Min();

        if (missing is not { } clubId)
        {
            return;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Club {clubId} plays league matches in {DivisionOf(division)} but has no Standings row there. ")
            + $"Fix the Standings, or {Reset}.");
    }

    /// <summary>
    /// Checks that every club hosted every other club exactly once, and reports the first pair that did not. The fix
    /// names the missing match when the pair never met, and the extra match when it met more than once.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="clubs">The Standings clubs in league team id order.</param>
    /// <param name="leagueMatches">The league matches.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a pair met more or fewer times than once.</exception>
    private static void CheckEveryPairMetOnce(
        GetCompetitionsResult division,
        List<int> clubs,
        IReadOnlyList<SeasonMatch> leagueMatches)
    {
        ILookup<(int Home, int Away), int> matchIdsOfPair = leagueMatches.ToLookup(
            match => (match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value),
            match => match.Id);

        foreach (int home in clubs)
        {
            foreach (int away in clubs.Where(clubId => clubId != home))
            {
                List<int> ids = matchIdsOfPair[(home, away)].Order().ToList();

                if (ids.Count == 1)
                {
                    continue;
                }

                string idsText = ids.Count == 0
                    ? "none"
                    : string.Join(", ", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

                string fix = ids.Count == 0 ? "Add the missing match" : "Delete the extra match";

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {home} hosts club {away} {ids.Count} times in {DivisionOf(division)} instead of ")
                    + $"once (matches {idsText}). {fix}, or {Reset}.");
            }
        }
    }

    /// <summary>
    /// Checks that one club's wins, draws and losses add up to the matches it played: home and away against every
    /// other club.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="row">The club's Standings row.</param>
    /// <param name="clubCount">The number of clubs in the Standings.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the row counts another number of matches.</exception>
    private static void CheckMatchesCounted(GetCompetitionsResult division, GetStandingsResult row, int clubCount)
    {
        int played = checked(2 * (clubCount - 1));
        long counted = (long)row.Wins + row.Draws + row.Losses;

        if (counted == played)
        {
            return;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Club {row.LeagueTeamId} has {row.Wins} wins, {row.Draws} draws and {row.Losses} losses in the ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"Standings of {DivisionOf(division)}, {counted} matches, but it played {played}. ")
            + "Fix its Standings row.");
    }

    /// <summary>
    /// Checks that one club's goals for and against equal the sums over its league matches. The goals of a match
    /// already count an opponent's own goal for the club it helped.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="row">The club's Standings row.</param>
    /// <param name="leagueMatches">The league matches.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the row's goals differ from the matches'.</exception>
    private static void CheckGoals(
        GetCompetitionsResult division,
        GetStandingsResult row,
        IReadOnlyList<SeasonMatch> leagueMatches)
    {
        int clubId = row.LeagueTeamId;
        int goalsFor = 0;
        int goalsAgainst = 0;

        foreach (SeasonMatch match in leagueMatches)
        {
            if (match.HomeLeagueTeamId == clubId)
            {
                goalsFor = checked(goalsFor + match.HomeGoals);
                goalsAgainst = checked(goalsAgainst + match.AwayGoals);
            }
            else if (match.AwayLeagueTeamId == clubId)
            {
                goalsFor = checked(goalsFor + match.AwayGoals);
                goalsAgainst = checked(goalsAgainst + match.HomeGoals);
            }
        }

        if (row.GoalsFor != goalsFor || row.GoalsAgainst != goalsAgainst)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Club {clubId} has {row.GoalsFor} goals for and {row.GoalsAgainst} against in the Standings of ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DivisionOf(division)}, but its league matches give {goalsFor} and {goalsAgainst}. ")
                + "Fix its Standings row.");
        }
    }
}
