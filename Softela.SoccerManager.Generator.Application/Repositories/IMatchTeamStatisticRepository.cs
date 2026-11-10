namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Writes the statistics of the clubs in a match. Every method runs inside the active transaction when there is one.
/// </summary>
public interface IMatchTeamStatisticRepository
{
    /// <summary>
    /// Inserts one club's statistics row for a match. The parameters mirror those of
    /// <c>dbo.InsertMatchTeamStatistic</c>.
    /// </summary>
    /// <param name="leagueTeamId">The identifier of the league team, the roster entry, the row belongs to.</param>
    /// <param name="matchId">The identifier of the match.</param>
    /// <param name="isHomeTeam">Whether the club plays the match at home.</param>
    /// <param name="shotsTotal">The number of shots.</param>
    /// <param name="shotsOnTarget">The number of shots on target.</param>
    /// <param name="possession">The share of possession.</param>
    /// <param name="createdAt">The UTC date and time the row is created.</param>
    /// <param name="modifiedAt">The UTC date and time the row is last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates the row.</param>
    /// <param name="modifiedBy">The identifier of the user that last modifies the row.</param>
    /// <returns>The identifier of the new row.</returns>
    Task<int> CreateAsync(
        int leagueTeamId,
        int matchId,
        bool isHomeTeam,
        int shotsTotal,
        int shotsOnTarget,
        int possession,
        DateTime createdAt,
        DateTime modifiedAt,
        Guid createdBy,
        Guid modifiedBy);
}
