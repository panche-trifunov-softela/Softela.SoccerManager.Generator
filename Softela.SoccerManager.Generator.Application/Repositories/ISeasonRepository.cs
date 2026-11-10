using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads seasons. Every method runs inside the active transaction when there is one.
/// </summary>
public interface ISeasonRepository
{
    /// <summary>
    /// Gets the seasons of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The seasons of the league.</returns>
    Task<IReadOnlyList<GetSeasonsResult>> GetByLeagueIdAsync(int leagueId);
}
