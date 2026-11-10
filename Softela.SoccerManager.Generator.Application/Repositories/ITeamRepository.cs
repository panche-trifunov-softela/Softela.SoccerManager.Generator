using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads teams. Every method runs inside the active transaction when there is one.
/// </summary>
public interface ITeamRepository
{
    /// <summary>
    /// Gets every team.
    /// </summary>
    /// <returns>Every team.</returns>
    Task<IReadOnlyList<GetTeamsResult>> GetAllAsync();
}
