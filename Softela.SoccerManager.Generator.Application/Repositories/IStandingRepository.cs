using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads standings. Every method runs inside the active transaction when there is one.
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
}
