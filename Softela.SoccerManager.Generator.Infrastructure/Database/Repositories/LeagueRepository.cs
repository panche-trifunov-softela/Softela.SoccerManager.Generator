using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads leagues through the backend's stored procedures.
/// </summary>
internal sealed class LeagueRepository : ILeagueRepository
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public LeagueRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets every league. The procedure's three filters default to NULL, which returns every league, newest first.
    /// </summary>
    /// <returns>Every league.</returns>
    public async Task<IReadOnlyList<GetLeaguesResult>> GetAllAsync()
    {
        var rows = await _context.Connection.QueryAsync<GetLeaguesResult>(
            "dbo.GetLeagues",
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }
}
