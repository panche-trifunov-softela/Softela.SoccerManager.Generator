namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// A finished match as a table counts it. The goals already include the opponent's own goals. A table never counts a
/// shootout, so there is none here. The caller passes only finished matches of the table: an unplayed match reads 0-0
/// and would count as a draw.
/// </summary>
/// <param name="HomeId">The id of the home participant.</param>
/// <param name="AwayId">The id of the away participant.</param>
/// <param name="HomeGoals">The goals the home participant is credited with.</param>
/// <param name="AwayGoals">The goals the away participant is credited with.</param>
public sealed record TableMatch(int HomeId, int AwayId, int HomeGoals, int AwayGoals);
