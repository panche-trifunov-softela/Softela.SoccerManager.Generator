using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads and creates standings rows through the backend's stored procedures.
/// </summary>
internal sealed class StandingRepository : IStandingRepository
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="StandingRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public StandingRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the standings rows of one competition in one season.
    /// </summary>
    /// <param name="competitionId">The identifier of the competition.</param>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <returns>The standings rows of the competition in the season.</returns>
    public async Task<IReadOnlyList<GetStandingsResult>> GetByCompetitionAndSeasonAsync(int competitionId, int seasonId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@CompetitionId", competitionId, DbType.Int32);
        parameters.Add("@SeasonId", seasonId, DbType.Int32);

        var rows = await _context.Connection.QueryAsync<GetStandingsResult>(
            "dbo.GetStandings",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }

    /// <summary>
    /// Inserts one standings row at zero: no points, goals, wins, draws or losses. It is the same for any competition;
    /// the backend refuses a club of another league and a guest in a division.
    /// </summary>
    /// <param name="competitionId">The identifier of the competition.</param>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <param name="leagueTeamId">The identifier of the league team, the roster entry, the row is for.</param>
    /// <param name="createdAt">The UTC date and time the row is created and last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates and last modifies the row.</param>
    /// <returns>The identifier of the new row.</returns>
    public async Task<int> CreateAsync(
        int competitionId,
        int seasonId,
        int leagueTeamId,
        DateTime createdAt,
        Guid createdBy)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@CompetitionId", competitionId, DbType.Int32);
        parameters.Add("@SeasonId", seasonId, DbType.Int32);
        parameters.Add("@LeagueTeamId", leagueTeamId, DbType.Int32);
        parameters.Add("@Points", 0, DbType.Int32);
        parameters.Add("@GoalsFor", 0, DbType.Int32);
        parameters.Add("@GoalsAgainst", 0, DbType.Int32);
        parameters.Add("@Wins", 0, DbType.Int32);
        parameters.Add("@Draws", 0, DbType.Int32);
        parameters.Add("@Losses", 0, DbType.Int32);
        parameters.Add("@CreatedAt", createdAt, DbType.DateTime2);
        parameters.Add("@ModifiedAt", createdAt, DbType.DateTime2);
        parameters.Add("@CreatedBy", createdBy, DbType.Guid);
        parameters.Add("@ModifiedBy", createdBy, DbType.Guid);

        return await _context.Connection.QuerySingleAsync<int>(
            "dbo.InsertStanding",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);
    }
}
