using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads competitions through the backend's stored procedures.
/// </summary>
internal sealed class CompetitionRepository : ICompetitionRepository
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompetitionRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public CompetitionRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the competitions of one league.
    /// </summary>
    /// <param name="leagueId">The identifier of the league.</param>
    /// <returns>The competitions of the league.</returns>
    public async Task<IReadOnlyList<GetCompetitionsResult>> GetByLeagueIdAsync(int leagueId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LeagueId", leagueId, DbType.Int32);

        var rows = await _context.Connection.QueryAsync<GetCompetitionsResult>(
            "dbo.GetCompetitions",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }
}
