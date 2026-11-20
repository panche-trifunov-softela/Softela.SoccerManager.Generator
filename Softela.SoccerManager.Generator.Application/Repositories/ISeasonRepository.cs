using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads and creates seasons. Every method runs inside the active transaction when there is one.
/// </summary>
public interface ISeasonRepository
{
    /// <summary>
    /// Gets the seasons of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The seasons of the league.</returns>
    Task<IReadOnlyList<GetSeasonsResult>> GetByLeagueIdAsync(int leagueId);

    /// <summary>
    /// Inserts one season through dbo.InsertSeason on the active transaction.
    /// </summary>
    /// <param name="leagueId">The identifier of the league the season belongs to.</param>
    /// <param name="seasonNumber">The number of the season in its league.</param>
    /// <param name="createdAt">The UTC date and time the row is created and last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates and last modifies the row.</param>
    /// <returns>
    /// The identifier of the new season, or null when the league already has a season with that number, which the
    /// database refuses with its unique key.
    /// </returns>
    Task<int?> CreateAsync(int leagueId, int seasonNumber, DateTime createdAt, Guid createdBy);
}
