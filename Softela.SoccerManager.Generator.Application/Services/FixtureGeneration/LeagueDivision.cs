namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// One division of the season being written.
/// </summary>
/// <param name="CompetitionId">The id of the division's competition; it is written as each match's competition.</param>
/// <param name="Name">The name of the competition, for log lines and refusals.</param>
/// <param name="LeagueTeamIds">The clubs of the division as league team ids, in ascending order.</param>
internal sealed record LeagueDivision(int CompetitionId, string Name, IReadOnlyList<int> LeagueTeamIds);
