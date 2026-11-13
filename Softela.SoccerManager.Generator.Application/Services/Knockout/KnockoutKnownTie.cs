namespace Softela.SoccerManager.Generator.Application.Services.Knockout;

/// <summary>
/// A cup tie whose two clubs are both known. The first and second club keep the bracket's side order.
/// </summary>
/// <param name="RoundNumber">The 1-based round the tie belongs to.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="FirstLeagueTeamId">The league team id of the club on the tie's first side.</param>
/// <param name="SecondLeagueTeamId">The league team id of the club on the tie's second side.</param>
public sealed record KnockoutKnownTie(
    int RoundNumber,
    int TieNumber,
    int FirstLeagueTeamId,
    int SecondLeagueTeamId);
