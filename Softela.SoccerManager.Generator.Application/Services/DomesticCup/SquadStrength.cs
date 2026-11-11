namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// The strength of one club's squad, measured by its best 16 players.
/// </summary>
/// <param name="LeagueTeamId">The league team id of the club.</param>
/// <param name="TeamId">The id of the club's team row.</param>
/// <param name="BestSixteenTotal">The sum of the ratings of the squad's 16 best players.</param>
/// <param name="SeventeenthRating">The rating of the squad's 17th best player, or 0 when there is none.</param>
public sealed record SquadStrength(int LeagueTeamId, int TeamId, long BestSixteenTotal, int SeventeenthRating)
{
    /// <summary>
    /// Gets the average rating of the best 16 players, for logs. Comparing totals compares averages, so the ranking
    /// never reads it.
    /// </summary>
    public decimal BestSixteenAverage => BestSixteenTotal / 16m;
}
