using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads national teams through the backend's stored procedures.
/// </summary>
internal sealed class NationalTeamRepository : INationalTeamRepository
{
    /// <summary>
    /// The number of the SQL Server error that a call of a stored procedure that does not exist raises.
    /// </summary>
    private const int MissingProcedureErrorNumber = 2812;

    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="NationalTeamRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public NationalTeamRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets every national team on the roster of one league, with the roster entry that is the nation in its World Cup
    /// matches.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The national teams on the league's roster, ordered by roster entry.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    public async Task<IReadOnlyList<GetLeagueNationalTeamsResult>> GetLeagueNationalTeamsAsync(int leagueId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueId", leagueId, DbType.Int32);

        return await QueryProcedureAsync<GetLeagueNationalTeamsResult>(
            "dbo.GetLeagueNationalTeams",
            parameters,
            "The backend read dbo.GetLeagueNationalTeams is missing; deploy the backend first. The step needs the "
            + "backend's scripts up to V1_0_0_69 and the procedure.").ConfigureAwait(false);
    }

    /// <summary>
    /// Gets every national team with its confederation, which is null for a national team that has none.
    /// </summary>
    /// <returns>Every national team.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    public async Task<IReadOnlyList<GetNationalTeamConfederationsResult>> GetConfederationsAsync()
    {
        return await QueryProcedureAsync<GetNationalTeamConfederationsResult>(
            "dbo.GetNationalTeamConfederations",
            null,
            "The backend read dbo.GetNationalTeamConfederations is missing; deploy the backend first. The step needs "
            + "backend script V1_0_0_66 and the procedure.").ConfigureAwait(false);
    }

    /// <summary>
    /// Gets every player who has a national team, with that team and the player's current rating, in one read.
    /// </summary>
    /// <returns>
    /// The players of the national teams, ordered by national team and then by rating, highest first.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the procedure is missing, that is when the backend has not been deployed yet.
    /// </exception>
    public async Task<IReadOnlyList<GetNationalTeamPlayerRatingsResult>> GetPlayerRatingsAsync()
    {
        return await QueryProcedureAsync<GetNationalTeamPlayerRatingsResult>(
            "dbo.GetNationalTeamPlayerRatings",
            null,
            "The backend read dbo.GetNationalTeamPlayerRatings is missing; deploy the backend first. The step needs "
            + "the backend's scripts up to V1_0_0_69 and the procedure.").ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one stored procedure on the active connection and transaction and reads its rows, turning the error of a
    /// missing procedure into a refusal, so that the translation is written once.
    /// </summary>
    /// <typeparam name="TRow">The type of a row the procedure returns.</typeparam>
    /// <param name="procedure">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters of the call, or <see langword="null"/> when it has none.</param>
    /// <param name="missingProcedureMessage">The reason of the refusal when the procedure is missing.</param>
    /// <returns>The rows the procedure returns.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the procedure is missing.</exception>
    private async Task<IReadOnlyList<TRow>> QueryProcedureAsync<TRow>(
        string procedure,
        DynamicParameters? parameters,
        string missingProcedureMessage)
    {
        try
        {
            var rows = await _context.Connection.QueryAsync<TRow>(
                procedure,
                parameters,
                transaction: _context.Transaction,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);

            return rows.AsList();
        }
        catch (SqlException exception) when (exception.Number == MissingProcedureErrorNumber)
        {
            throw new SeasonRefusedException(missingProcedureMessage);
        }
    }
}
