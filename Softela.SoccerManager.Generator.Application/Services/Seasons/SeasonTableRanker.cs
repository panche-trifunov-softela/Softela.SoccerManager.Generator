using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Ranking;

namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// Ranks a table of clubs from a season's finished matches and hands back the club ids in place order. The promotion
/// playoffs, the continental knockout, the season rollover and the continental entrants all rank a club table the same
/// way, so the ranking and the refusal that wraps the ranking library's complaint live here once. A division's table,
/// whose clubs its Standings rows name, is ranked here too.
/// </summary>
internal static class SeasonTableRanker
{
    /// <summary>
    /// Ranks a division's table from the finished matches of its league. The clubs of the table are the ones the
    /// division's Standings rows name.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the tie-break shuffle.</param>
    /// <param name="divisionText">
    /// The division as a refusal names it, for example from <c>PlayoffTableCheck.DivisionOf</c>.
    /// </param>
    /// <param name="standings">The division's Standings rows, which name the clubs and have been checked.</param>
    /// <param name="finishedMatches">The division's league matches, all finished and with both clubs.</param>
    /// <param name="fix">The end of the refusal's message: what to do, as a sentence.</param>
    /// <returns>The league team ids in place order, place 1 first.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the ranking library refuses the matches.</exception>
    internal static List<int> RankDivision(
        int seasonId,
        string divisionText,
        IReadOnlyList<GetStandingsResult> standings,
        IReadOnlyList<SeasonMatch> finishedMatches,
        string fix)
    {
        List<int> clubs = standings.Select(row => row.LeagueTeamId).Distinct().Order().ToList();

        return RankIds(
            seasonId,
            clubs,
            finishedMatches,
            $"The table of {divisionText} refused its league matches",
            fix);
    }

    /// <summary>
    /// Ranks a club table, in the club order of the ranking library, from the finished matches of the table.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the tie-break shuffle.</param>
    /// <param name="clubIds">The league team ids of every club of the table.</param>
    /// <param name="finishedMatches">The finished matches of the table, all with both clubs, and no others.</param>
    /// <param name="refusal">The start of the refusal's message, which names the table; a colon follows it.</param>
    /// <param name="fix">The end of the refusal's message: what to do, as a sentence.</param>
    /// <returns>The league team ids in place order, place 1 first.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the ranking library refuses the matches: a club listed twice, or a match with a side that is not a
    /// club of the table, the same club on both sides, or negative goals.
    /// </exception>
    internal static List<int> RankIds(
        int seasonId,
        IReadOnlyList<int> clubIds,
        IReadOnlyList<SeasonMatch> finishedMatches,
        string refusal,
        string fix)
    {
        try
        {
            return TableRanking.Rank(seasonId, clubIds, SeasonTableMatches.Of(finishedMatches))
                .OrderBy(row => row.Place)
                .Select(row => row.Id)
                .ToList();
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            throw new SeasonRefusedException($"{refusal}: {DomesticCupReader.ReasonOf(exception)} {fix}");
        }
    }
}
