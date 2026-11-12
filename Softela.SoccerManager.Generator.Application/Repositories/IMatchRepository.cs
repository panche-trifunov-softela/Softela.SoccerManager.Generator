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
    /// Gets every match of one season, friendlies included, ordered by kickoff and then by id. Each match carries
    /// whether it is finished and its stage, round and leg.
    /// </summary>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <returns>The matches of the season.</returns>
    Task<IReadOnlyList<GetMatchesBySeasonResult>> GetBySeasonIdAsync(int seasonId);

    /// <summary>
    /// Gets the score of every club in every match of one season that has match statistic rows, ordered by kickoff,
    /// then by match id, with the home club first. An unplayed match reads 0-0, so whether it is finished comes from
    /// <see cref="GetBySeasonIdAsync"/>.
    /// </summary>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <returns>The scores of the season, one per club per match.</returns>
    Task<IReadOnlyList<GetMatchScoresBySeasonResult>> GetScoresBySeasonIdAsync(int seasonId);

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

    /// <summary>
    /// Inserts one staged match, a match with a stage: a cup round, a playoff or a continental stage. The parameters
    /// mirror those of <c>dbo.InsertStagedMatch</c>.
    /// </summary>
    /// <param name="seasonId">The identifier of the season the match belongs to.</param>
    /// <param name="competitionId">The identifier of the competition the match is played in.</param>
    /// <param name="refereeId">The identifier of the referee, or <see langword="null"/> when none is assigned.</param>
    /// <param name="startDateTime">The UTC kickoff of the match.</param>
    /// <param name="commentary">The commentary as JSON, or <see langword="null"/> when there is none.</param>
    /// <param name="attendance">The number of spectators.</param>
    /// <param name="isStarted">Whether the match has started.</param>
    /// <param name="stage">The stage the match belongs to, as the backend's match stage code.</param>
    /// <param name="roundNumber">The round of the stage, or <see langword="null"/> when it has no rounds.</param>
    /// <param name="leg">The leg of the tie, or <see langword="null"/> for a single match.</param>
    /// <param name="createdAt">The UTC date and time the row is created.</param>
    /// <param name="modifiedAt">The UTC date and time the row is last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates the row.</param>
    /// <param name="modifiedBy">The identifier of the user that last modifies the row.</param>
    /// <returns>The identifier of the new match.</returns>
    Task<int> CreateStagedAsync(
        int seasonId,
        int competitionId,
        int? refereeId,
        DateTime startDateTime,
        string? commentary,
        int attendance,
        bool isStarted,
        byte stage,
        byte? roundNumber,
        byte? leg,
        DateTime createdAt,
        DateTime modifiedAt,
        Guid createdBy,
        Guid modifiedBy);
}
