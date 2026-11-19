namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// One group of a World Cup round robin, in the qualifiers or in the finals: the nations that play each other.
/// </summary>
/// <param name="GroupNumber">The number of the group, from 1.</param>
/// <param name="LeagueTeamIds">The roster entries of the group's nations.</param>
/// <param name="Legs">The number of times each pair meets: 2 (home and away), or 1 (once).</param>
internal sealed record WorldCupGroup(
    byte GroupNumber,
    IReadOnlyList<int> LeagueTeamIds,
    int Legs);
