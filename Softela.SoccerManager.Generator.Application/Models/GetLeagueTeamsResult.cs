namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetLeagueTeams</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others.
/// </summary>
public sealed class GetLeagueTeamsResult
{
    /// <summary>
    /// The identifier of the league team, the roster entry; Standings and match statistics refer to this id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The identifier of the club's row in <c>dbo.Teams</c>.
    /// </summary>
    public int TeamId { get; set; }
}
