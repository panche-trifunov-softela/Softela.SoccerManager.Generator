using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads teams through the backend's stored procedures.
/// </summary>
internal sealed class TeamRepository : ITeamRepository
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public TeamRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets every team.
    /// </summary>
    /// <returns>Every team.</returns>
    public async Task<IReadOnlyList<GetTeamsResult>> GetAllAsync()
    {
        var rows = await _context.Connection.QueryAsync<GetTeamsResult>(
            "dbo.GetTeams",
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }
}
