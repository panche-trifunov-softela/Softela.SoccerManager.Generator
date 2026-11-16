namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// One knockout match of a continental cup that is ready to be written.
/// </summary>
/// <param name="Kickoff">The kickoff of the match, in UTC.</param>
/// <param name="Round">The round of the tie the match belongs to.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="Leg">The leg, 1 or 2; null for the final, which is a single match.</param>
/// <param name="HomeLeagueTeamId">The league team id of the home club.</param>
/// <param name="AwayLeagueTeamId">The league team id of the away club.</param>
public sealed record ContinentalPlannedLeg(
    DateTime Kickoff,
    ContinentalRound Round,
    int TieNumber,
    byte? Leg,
    int HomeLeagueTeamId,
    int AwayLeagueTeamId);
