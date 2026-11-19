using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// One group of the World Cup qualifiers as its written matches show it: who plays in it, how often each pair meets and
/// the matches themselves.
/// </summary>
/// <param name="GroupNumber">The number of the group, as its matches carry it.</param>
/// <param name="Confederation">The confederation every nation of the group belongs to.</param>
/// <param name="LeagueTeamIds">The roster entries of the group's nations, in ascending order.</param>
/// <param name="Legs">The number of times each pair meets: 1 for a group of 5, otherwise 2.</param>
/// <param name="Matches">The group's matches, in ascending order of match id.</param>
internal sealed record WrittenQualifierGroup(
    byte GroupNumber,
    Confederation Confederation,
    IReadOnlyList<int> LeagueTeamIds,
    int Legs,
    IReadOnlyList<SeasonMatch> Matches);
