namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetLeagueSquadRatings</c> stored procedure: a squad player of a club on a league's
/// roster, with the player's current rating. A player registered twice in the same club appears twice, with the same
/// rating. It holds only the columns the generator reads; Dapper ignores the others.
/// </summary>
public sealed class GetLeagueSquadRatingsResult
{
    /// <summary>
    /// The identifier of the league team, the roster entry, whose squad the player belongs to.
    /// </summary>
    public int LeagueTeamId { get; set; }

    /// <summary>
    /// The identifier of the player.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The current rating of the player, or <see langword="null"/> when the player has none.
    /// </summary>
    public int? Rating { get; set; }
}
