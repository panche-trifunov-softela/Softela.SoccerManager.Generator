namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// A cup tie that has been decided.
/// </summary>
/// <param name="RoundNumber">The 1-based round of the decided tie.</param>
/// <param name="TieNumber">The 1-based number of the decided tie within its round.</param>
/// <param name="LeagueTeamId">The league team id of the club that won the tie.</param>
public sealed record DomesticCupWinner(int RoundNumber, int TieNumber, int LeagueTeamId);
