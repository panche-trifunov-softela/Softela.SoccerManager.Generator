using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads and creates standings rows. Every method runs inside the active transaction when there is one.
/// </summary>
public interface IStandingRepository
{
    /// <summary>
    /// Gets the standings rows of one competition in one season.
    /// </summary>
    /// <param name="competitionId">The identifier of the competition.</param>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <returns>The standings rows of the competition in the season.</returns>
    Task<IReadOnlyList<GetStandingsResult>> GetByCompetitionAndSeasonAsync(int competitionId, int seasonId);

    /// <summary>
    /// Inserts one standings row at zero: no points, goals, wins, draws or losses. It is the same for any competition;
    /// the backend refuses a club of another league and a guest in a division.
    /// </summary>
    /// <param name="competitionId">The identifier of the competition.</param>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <param name="leagueTeamId">The identifier of the league team, the roster entry, the row is for.</param>
    /// <param name="createdAt">The UTC date and time the row is created and last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates and last modifies the row.</param>
    /// <returns>The identifier of the new row.</returns>
    Task<int> CreateAsync(int competitionId, int seasonId, int leagueTeamId, DateTime createdAt, Guid createdBy);
}
