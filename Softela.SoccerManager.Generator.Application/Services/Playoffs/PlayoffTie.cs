namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// One tie of a division's promotion playoff: a single match between two clubs, one of which hosts it.
/// </summary>
/// <param name="Stage">
/// The match stage of the tie: <see cref="PlayoffStages.SemiFinal"/> or <see cref="PlayoffStages.Final"/>.
/// </param>
/// <param name="TieNumber">
/// The 1-based number of the tie within its stage: 1 and 2 for the semi-finals, 1 for the final.
/// </param>
/// <param name="HomeLeagueTeamId">The league team id of the club that hosts the match.</param>
/// <param name="AwayLeagueTeamId">The league team id of the club that visits.</param>
/// <param name="HomePlace">The 1-based place of the home club in the division's table.</param>
/// <param name="AwayPlace">The 1-based place of the away club in the division's table.</param>
public sealed record PlayoffTie(
    byte Stage,
    int TieNumber,
    int HomeLeagueTeamId,
    int AwayLeagueTeamId,
    int HomePlace,
    int AwayPlace);
