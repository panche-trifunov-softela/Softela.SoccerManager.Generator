using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Reads and writes matches. Every method runs inside the active transaction when there is one.
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// Gets every match of every league.
    /// </summary>
    /// <returns>Every match.</returns>
    Task<IReadOnlyList<GetMatchesResult>> GetAllAsync();

    /// <summary>
    /// Inserts one match. The parameters mirror those of <c>dbo.InsertMatch</c>.
    /// </summary>
    /// <param name="seasonId">The identifier of the season the match belongs to.</param>
    /// <param name="competitionId">The identifier of the competition, or <see langword="null"/> for a friendly.</param>
    /// <param name="refereeId">The identifier of the referee, or <see langword="null"/> when none is assigned.</param>
    /// <param name="startDateTime">The UTC kickoff of the match.</param>
    /// <param name="commentary">The commentary as JSON, or <see langword="null"/> when there is none.</param>
    /// <param name="attendance">The number of spectators.</param>
    /// <param name="isStarted">Whether the match has started.</param>
    /// <param name="createdAt">The UTC date and time the row is created.</param>
    /// <param name="modifiedAt">The UTC date and time the row is last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates the row.</param>
    /// <param name="modifiedBy">The identifier of the user that last modifies the row.</param>
    /// <returns>The identifier of the new match.</returns>
    Task<int> CreateAsync(
        int seasonId,
        int? competitionId,
        int? refereeId,
        DateTime startDateTime,
        string? commentary,
        int attendance,
        bool isStarted,
        DateTime createdAt,
        DateTime modifiedAt,
        Guid createdBy,
        Guid modifiedBy);
}
