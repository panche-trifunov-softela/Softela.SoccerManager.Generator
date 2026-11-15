using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads the rosters of leagues and the squads of their clubs. Every method runs inside the active transaction when
/// there is one.
/// </summary>
public interface ILeagueTeamRepository
{
    /// <summary>
    /// Gets the roster of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The league teams, the roster entries, of the league.</returns>
    Task<IReadOnlyList<GetLeagueTeamsResult>> GetByLeagueIdAsync(int leagueId);

    /// <summary>
    /// Gets the squad of one club: its players with their current ratings. A player registered twice in the club
    /// appears twice.
    /// </summary>
    /// <param name="leagueTeamId">The identifier of the league team, the roster entry.</param>
    /// <returns>The players of the squad.</returns>
    Task<IReadOnlyList<GetLeagueTeamSquadResult>> GetSquadAsync(int leagueTeamId);

    /// <summary>
    /// Gets every squad player of every club on the roster of one league, with the current rating, in one read.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The squad rows of the league, ordered by league team and then by player.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    Task<IReadOnlyList<GetLeagueSquadRatingsResult>> GetLeagueSquadRatingsAsync(int leagueId);
}
