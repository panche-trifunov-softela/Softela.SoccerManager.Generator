using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Writes the statistics of the clubs in a match through the backend's stored procedures.
/// </summary>
internal sealed class MatchTeamStatisticRepository : IMatchTeamStatisticRepository
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchTeamStatisticRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public MatchTeamStatisticRepository(IDapperDataContext context)
    {
        _context = context;
    }

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
    public async Task<int> CreateAsync(
        int leagueTeamId,
        int matchId,
        bool isHomeTeam,
        int shotsTotal,
        int shotsOnTarget,
        int possession,
        DateTime createdAt,
        DateTime modifiedAt,
        Guid createdBy,
        Guid modifiedBy)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueTeamId", leagueTeamId, DbType.Int32);
        parameters.Add("@MatchId", matchId, DbType.Int32);
        parameters.Add("@IsHomeTeam", isHomeTeam, DbType.Boolean);
        parameters.Add("@ShotsTotal", shotsTotal, DbType.Int32);
        parameters.Add("@ShotsOnTarget", shotsOnTarget, DbType.Int32);
        parameters.Add("@Possession", possession, DbType.Int32);
        parameters.Add("@CreatedAt", createdAt, DbType.DateTime2);
        parameters.Add("@ModifiedAt", modifiedAt, DbType.DateTime2);
        parameters.Add("@CreatedBy", createdBy, DbType.Guid);
        parameters.Add("@ModifiedBy", modifiedBy, DbType.Guid);

        return await _context.Connection.QuerySingleAsync<int>(
            "dbo.InsertMatchTeamStatistic",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);
    }
}
