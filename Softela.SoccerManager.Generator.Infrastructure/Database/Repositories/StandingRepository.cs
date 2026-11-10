using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads standings through the backend's stored procedures.
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
}
