namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetLeagueTeamSquad</c> stored procedure: a player of a club's squad. A player
/// registered twice in the same club appears twice, with the same rating. It holds only the columns the generator
/// reads; Dapper ignores the others.
/// </summary>
public sealed class GetLeagueTeamSquadResult
{
    /// <summary>
    /// The identifier of the player.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The current rating of the player, or <see langword="null"/> when the player has none.
    /// </summary>
    public int? Rating { get; set; }
}
