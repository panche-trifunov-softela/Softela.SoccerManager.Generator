using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads national teams: their place on a league's roster, their confederations and the ratings of their players. It
/// only reads. Every method runs inside the active transaction when there is one.
/// </summary>
public interface INationalTeamRepository
{
    /// <summary>
    /// Gets every national team on the roster of one league, with the roster entry that is the nation in its World Cup
    /// matches.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The national teams on the league's roster, ordered by roster entry.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    Task<IReadOnlyList<GetLeagueNationalTeamsResult>> GetLeagueNationalTeamsAsync(int leagueId);

    /// <summary>
    /// Gets every national team with its confederation, which is null for a national team that has none.
    /// </summary>
    /// <returns>Every national team.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    Task<IReadOnlyList<GetNationalTeamConfederationsResult>> GetConfederationsAsync();

    /// <summary>
    /// Gets every player who has a national team, with that team and the player's current rating, in one read.
    /// </summary>
    /// <returns>
    /// The players of the national teams, ordered by national team and then by rating, highest first.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    Task<IReadOnlyList<GetNationalTeamPlayerRatingsResult>> GetPlayerRatingsAsync();
}
