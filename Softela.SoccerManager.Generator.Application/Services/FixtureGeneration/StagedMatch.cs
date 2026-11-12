using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// One staged match to insert: a match that belongs to a stage of a competition, such as a round of the domestic cup.
/// </summary>
/// <param name="CompetitionId">The id of the match's competition.</param>
/// <param name="Kickoff">The kickoff of the match, in UTC.</param>
/// <param name="Stage">The stage the match belongs to.</param>
/// <param name="RoundNumber">The 1-based round of the stage the match is played in.</param>
/// <param name="Leg">The leg of a two-legged tie, or null for a single match.</param>
/// <param name="HomeLeagueTeamId">The league team id of the home club.</param>
/// <param name="AwayLeagueTeamId">The league team id of the away club.</param>
internal sealed record StagedMatch(
    int CompetitionId,
    DateTime Kickoff,
    MatchStage Stage,
    byte RoundNumber,
    byte? Leg,
    int HomeLeagueTeamId,
    int AwayLeagueTeamId);
