using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads competitions. Every method runs inside the active transaction when there is one.
/// </summary>
public interface ICompetitionRepository
{
    /// <summary>
    /// Gets the competitions of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The competitions of the league.</returns>
    Task<IReadOnlyList<GetCompetitionsResult>> GetByLeagueIdAsync(int leagueId);
}
