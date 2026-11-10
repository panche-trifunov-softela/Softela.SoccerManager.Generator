namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetStandings</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others.
/// </summary>
public sealed class GetStandingsResult
{
    /// <summary>
    /// The identifier of the league team, the roster entry, the standing belongs to.
    /// </summary>
    public int LeagueTeamId { get; set; }

    /// <summary>
    /// The UTC date and time the row was last written. It equals the creation time until the row is edited.
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}
