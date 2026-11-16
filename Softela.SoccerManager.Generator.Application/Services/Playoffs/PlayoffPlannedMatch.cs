namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// One match of a division's promotion playoff that is ready to be written.
/// </summary>
/// <param name="Kickoff">The kickoff of the match, in UTC.</param>
/// <param name="Stage">
/// The match stage: <see cref="PlayoffStages.SemiFinal"/> or <see cref="PlayoffStages.Final"/>.
/// </param>
/// <param name="TieNumber">
/// The 1-based number of the tie within its stage: 1 and 2 for the semi-finals, 1 for the final.
/// </param>
/// <param name="HomeLeagueTeamId">The league team id of the home club.</param>
/// <param name="AwayLeagueTeamId">The league team id of the away club.</param>
/// <param name="HomePlace">The 1-based place of the home club in the division's table.</param>
/// <param name="AwayPlace">The 1-based place of the away club in the division's table.</param>
public sealed record PlayoffPlannedMatch(
    DateTime Kickoff,
    byte Stage,
    int TieNumber,
    int HomeLeagueTeamId,
    int AwayLeagueTeamId,
    int HomePlace,
    int AwayPlace);
