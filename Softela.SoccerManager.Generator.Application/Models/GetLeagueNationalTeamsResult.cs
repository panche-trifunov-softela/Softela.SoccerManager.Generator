namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetLeagueNationalTeams</c> stored procedure: a national team on a league's roster. It
/// holds only the columns the generator reads; Dapper ignores the others.
/// </summary>
public sealed class GetLeagueNationalTeamsResult
{
    /// <summary>
    /// The identifier of the league team, the roster entry, that is the nation in its World Cup matches.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The identifier of the national team the roster entry stands for.
    /// </summary>
    public int NationalTeamId { get; set; }
}
