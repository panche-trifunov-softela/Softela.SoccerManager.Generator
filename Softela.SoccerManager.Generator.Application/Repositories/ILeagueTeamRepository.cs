using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads the rosters of leagues. Every method runs inside the active transaction when there is one.
/// </summary>
public interface ILeagueTeamRepository
{
    /// <summary>
    /// Gets the roster of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The league teams, the roster entries, of the league.</returns>
    Task<IReadOnlyList<GetLeagueTeamsResult>> GetByLeagueIdAsync(int leagueId);
}
