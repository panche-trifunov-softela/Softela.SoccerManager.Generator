using System.Data;
using Dapper;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Repositories;

/// <summary>
/// Reads and writes matches through the backend's stored procedures.
/// </summary>
internal sealed class MatchRepository : IMatchRepository
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchRepository"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction the calls use.</param>
    public MatchRepository(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets every match of every league.
    /// </summary>
    /// <returns>Every match.</returns>
    public async Task<IReadOnlyList<GetMatchesResult>> GetAllAsync()
    {
        var rows = await _context.Connection.QueryAsync<GetMatchesResult>(
            "dbo.GetMatches",
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }

    /// <summary>
    /// Gets every match of one season, friendlies included, ordered by kickoff and then by id.
    /// </summary>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <returns>The matches of the season.</returns>
    public async Task<IReadOnlyList<GetMatchesResult>> GetBySeasonIdAsync(int seasonId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SeasonId", seasonId, DbType.Int32);

        var rows = await _context.Connection.QueryAsync<GetMatchesResult>(
            "dbo.GetMatchesBySeason",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return rows.AsList();
    }

    /// <summary>
    /// Inserts one match. The parameters mirror those of <c>dbo.InsertMatch</c>, which has no defaults.
    /// </summary>
    /// <param name="seasonId">The identifier of the season the match belongs to.</param>
    /// <param name="competitionId">The identifier of the competition, or <see langword="null"/> for a friendly.</param>
    /// <param name="refereeId">The identifier of the referee, or <see langword="null"/> when none is assigned.</param>
    /// <param name="startDateTime">The UTC kickoff of the match.</param>
    /// <param name="commentary">The commentary as JSON, or <see langword="null"/> when there is none.</param>
    /// <param name="attendance">The number of spectators.</param>
    /// <param name="isStarted">Whether the match has started.</param>
    /// <param name="createdAt">The UTC date and time the row is created.</param>
    /// <param name="modifiedAt">The UTC date and time the row is last modified.</param>
    /// <param name="createdBy">The identifier of the user that creates the row.</param>
    /// <param name="modifiedBy">The identifier of the user that last modifies the row.</param>
    /// <returns>The identifier of the new match.</returns>
    public async Task<int> CreateAsync(
        int seasonId,
        int? competitionId,
        int? refereeId,
        DateTime startDateTime,
        string? commentary,
        int attendance,
        bool isStarted,
        DateTime createdAt,
        DateTime modifiedAt,
        Guid createdBy,
        Guid modifiedBy)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SeasonId", seasonId, DbType.Int32);
        parameters.Add("@CompetitionId", competitionId, DbType.Int32);
        parameters.Add("@RefereeId", refereeId, DbType.Int32);
        parameters.Add("@StartDateTime", startDateTime, DbType.DateTime2);
        parameters.Add("@Commentary", commentary, DbType.String);
        parameters.Add("@Attendance", attendance, DbType.Int32);
        parameters.Add("@IsStarted", isStarted, DbType.Boolean);
        parameters.Add("@CreatedAt", createdAt, DbType.DateTime2);
        parameters.Add("@ModifiedAt", modifiedAt, DbType.DateTime2);
        parameters.Add("@CreatedBy", createdBy, DbType.Guid);
        parameters.Add("@ModifiedBy", modifiedBy, DbType.Guid);

        return await _context.Connection.QuerySingleAsync<int>(
            "dbo.InsertMatch",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);
    }
}
