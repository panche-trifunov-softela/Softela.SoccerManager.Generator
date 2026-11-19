namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// One group of the World Cup qualifiers: the nations that play each other, from one confederation.
/// </summary>
/// <param name="GroupNumber">
/// The number of the group, counted from 1 over the whole draw: the confederations in their order, and each
/// confederation's groups in the order they were drawn.
/// </param>
/// <param name="Confederation">The confederation the group's nations belong to.</param>
/// <param name="LeagueTeamIds">The roster entries of the group's nations, in pot order.</param>
/// <param name="Legs">The number of times each pair meets: 2 (home and away), or 1 for a group of 5.</param>
internal sealed record QualifierGroup(
    byte GroupNumber,
    Confederation Confederation,
    IReadOnlyList<int> LeagueTeamIds,
    int Legs);
