using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads and creates seasons through the backend's stored procedures.
/// </summary>
internal sealed class SeasonRepository : ISeasonRepository
{
    /// <summary>
    /// The number of the SQL Server error that a violated unique key raises: here, a league that already has a season
    /// with the number.
    /// </summary>
    private const int UniqueKeyErrorNumber = 2627;

    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public SeasonRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the seasons of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The seasons of the league.</returns>
    public async Task<IReadOnlyList<GetSeasonsResult>> GetByLeagueIdAsync(int leagueId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueId", leagueId, DbType.Int32);

        var rows = await _context.Connection.QueryAsync<GetSeasonsResult>(
            "dbo.GetSeasons",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }

    /// <summary>
    /// Inserts one season through dbo.InsertSeason on the active transaction. The Generator never sets XACT_ABORT, so
    /// after the unique key refuses a season the transaction is still open and the caller's rollback is clean.
    /// </summary>
    /// <param name="leagueId">The identifier of the league the season belongs to.</param>
    /// <param name="seasonNumber">The number of the season in its league.</param>
    /// <param name="createdAt">The UTC date and time the row is created and last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates and last modifies the row.</param>
    /// <returns>
    /// The identifier of the new season, or null when the league already has a season with that number.
    /// </returns>
    public async Task<int?> CreateAsync(int leagueId, int seasonNumber, DateTime createdAt, Guid createdBy)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueId", leagueId, DbType.Int32);
        parameters.Add("@SeasonNumber", seasonNumber, DbType.Int32);
        parameters.Add("@CreatedAt", createdAt, DbType.DateTime2);
        parameters.Add("@ModifiedAt", createdAt, DbType.DateTime2);
        parameters.Add("@CreatedBy", createdBy, DbType.Guid);
        parameters.Add("@ModifiedBy", createdBy, DbType.Guid);

        try
        {
            return await _context.Connection.QuerySingleAsync<int>(
                "dbo.InsertSeason",
                parameters,
                transaction: _context.Transaction,
                commandType: CommandType.StoredProcedure).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number == UniqueKeyErrorNumber)
        {
            return null;
        }
    }
}
