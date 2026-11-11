using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// What every step of a tick gets for one league: the league, its newest season and its divisions, and what the tick
/// read at its start, before any step ran.
/// </summary>
/// <param name="League">The league.</param>
/// <param name="Season">The newest season of the league, the one with the highest SeasonNumber.</param>
/// <param name="Divisions">
/// The league's competitions with an Order above 0, in ascending Order; there is at least one.
/// </param>
/// <param name="Teams">Every team, as read at the start of the tick.</param>
/// <param name="LeagueMatches">
/// The matches of all of the league's seasons, friendlies included, as read at the start of the tick. They do not show
/// what an earlier step of the same tick wrote.
/// </param>
/// <param name="RunTime">The moment of the run, in UTC.</param>
internal sealed record LeagueStepContext(
    GetLeaguesResult League,
    GetSeasonsResult Season,
    IReadOnlyList<GetCompetitionsResult> Divisions,
    IReadOnlyList<GetTeamsResult> Teams,
    IReadOnlyList<GetMatchesResult> LeagueMatches,
    DateTime RunTime)
{
    /// <summary>
    /// The start of every log line about the league's season. Its placeholders take the league's name and id and the
    /// season's number and id, in that order.
    /// </summary>
    internal const string LogPrefix = "League '{LeagueName}' (id {LeagueId}), season {SeasonNumber} (id {SeasonId})";
}
