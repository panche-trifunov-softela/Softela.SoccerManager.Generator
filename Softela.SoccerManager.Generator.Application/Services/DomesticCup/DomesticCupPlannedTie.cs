namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// A cup tie that is ready to be written as a match.
/// </summary>
/// <param name="RoundNumber">The 1-based round of the tie.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="Kickoff">The kickoff of the match, in UTC.</param>
/// <param name="HomeLeagueTeamId">The league team id of the home club, which is the weaker squad.</param>
/// <param name="AwayLeagueTeamId">The league team id of the away club.</param>
public sealed record DomesticCupPlannedTie(
    int RoundNumber,
    int TieNumber,
    DateTime Kickoff,
    int HomeLeagueTeamId,
    int AwayLeagueTeamId);
