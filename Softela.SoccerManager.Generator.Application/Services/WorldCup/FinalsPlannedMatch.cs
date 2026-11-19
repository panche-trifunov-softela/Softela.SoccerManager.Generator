namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// One knockout match of the World Cup finals that is ready to be written.
/// </summary>
/// <param name="Kickoff">The kickoff of the match, in UTC.</param>
/// <param name="Round">The round the match belongs to.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round; 1 for the third place.</param>
/// <param name="HomeLeagueTeamId">The league team id of the home nation.</param>
/// <param name="AwayLeagueTeamId">The league team id of the away nation.</param>
internal sealed record FinalsPlannedMatch(
    DateTime Kickoff,
    FinalsRound Round,
    int TieNumber,
    int HomeLeagueTeamId,
    int AwayLeagueTeamId);
