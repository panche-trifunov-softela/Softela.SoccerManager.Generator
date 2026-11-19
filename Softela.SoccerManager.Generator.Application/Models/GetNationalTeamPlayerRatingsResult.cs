namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetNationalTeamPlayerRatings</c> stored procedure: a player who has a national team,
/// with the player's current rating. It holds only the columns the generator reads; Dapper ignores the others.
/// </summary>
public sealed class GetNationalTeamPlayerRatingsResult
{
    /// <summary>
    /// The identifier of the national team the player belongs to.
    /// </summary>
    public int NationalTeamId { get; set; }

    /// <summary>
    /// The current rating of the player, or <see langword="null"/> when the player has none.
    /// </summary>
    public int? Rating { get; set; }
}
