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

    /// <summary>
    /// The points the club has in the standing.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// The goals the club has scored in the standing.
    /// </summary>
    public int GoalsFor { get; set; }

    /// <summary>
    /// The goals the club has conceded in the standing.
    /// </summary>
    public int GoalsAgainst { get; set; }

    /// <summary>
    /// The matches the club has won in the standing.
    /// </summary>
    public int Wins { get; set; }

    /// <summary>
    /// The matches the club has drawn in the standing.
    /// </summary>
    public int Draws { get; set; }

    /// <summary>
    /// The matches the club has lost in the standing.
    /// </summary>
    public int Losses { get; set; }
}
