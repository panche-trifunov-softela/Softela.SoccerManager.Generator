namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// A club that enters the domestic cup, with the division it plays in.
/// </summary>
/// <param name="LeagueTeamId">The league team id of the club.</param>
/// <param name="DivisionRank">The 1-based rank of the club's division; 1 is the top division.</param>
public sealed record DomesticCupEntrant(int LeagueTeamId, int DivisionRank);
