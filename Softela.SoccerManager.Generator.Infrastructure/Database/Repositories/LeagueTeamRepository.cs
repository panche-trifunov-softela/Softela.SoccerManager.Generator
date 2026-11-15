using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads the rosters of leagues and the squads of their clubs through the backend's stored procedures.
/// </summary>
internal sealed class LeagueTeamRepository : ILeagueTeamRepository
{
    /// <summary>
    /// The number of the SQL Server error that a call of a stored procedure that does not exist raises.
    /// </summary>
    private const int MissingProcedureErrorNumber = 2812;

    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueTeamRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public LeagueTeamRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the roster of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The league teams, the roster entries, of the league.</returns>
    public async Task<IReadOnlyList<GetLeagueTeamsResult>> GetByLeagueIdAsync(int leagueId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueId", leagueId, DbType.Int32);

        var rows = await _context.Connection.QueryAsync<GetLeagueTeamsResult>(
            "dbo.GetLeagueTeams",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }

    /// <summary>
    /// Gets the squad of one club: its players with their current ratings. A player registered twice in the club
    /// appears twice.
    /// </summary>
    /// <param name="leagueTeamId">The identifier of the league team, the roster entry.</param>
    /// <returns>The players of the squad.</returns>
    public async Task<IReadOnlyList<GetLeagueTeamSquadResult>> GetSquadAsync(int leagueTeamId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueTeamId", leagueTeamId, DbType.Int32);

        var rows = await _context.Connection.QueryAsync<GetLeagueTeamSquadResult>(
            "dbo.GetLeagueTeamSquad",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }

    /// <summary>
    /// Gets every squad player of every club on the roster of one league, with the current rating, in one read.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The squad rows of the league, ordered by league team and then by player.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    public async Task<IReadOnlyList<GetLeagueSquadRatingsResult>> GetLeagueSquadRatingsAsync(int leagueId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueId", leagueId, DbType.Int32);

        try
        {
            var rows = await _context.Connection.QueryAsync<GetLeagueSquadRatingsResult>(
                "dbo.GetLeagueSquadRatings",
                parameters,
                transaction: _context.Transaction,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);

            return rows.AsList();
        }
        catch (SqlException exception) when (exception.Number == MissingProcedureErrorNumber)
        {
            throw new SeasonRefusedException(
                "The backend read dbo.GetLeagueSquadRatings is missing; deploy the backend first. The step needs "
                + "backend script V1_0_0_65 and the procedure.");
        }
    }
}
