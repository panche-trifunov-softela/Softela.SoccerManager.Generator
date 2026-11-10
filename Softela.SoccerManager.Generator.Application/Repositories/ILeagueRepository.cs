using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads leagues. Every method runs inside the active transaction when there is one.
/// </summary>
public interface ILeagueRepository
{
    /// <summary>
    /// Gets every league, since the stored procedure's filters are all left NULL. The result keeps the procedure's
    /// order, newest first, so callers sort it.
    /// </summary>
    /// <returns>Every league.</returns>
    Task<IReadOnlyList<GetLeaguesResult>> GetAllAsync();
}
