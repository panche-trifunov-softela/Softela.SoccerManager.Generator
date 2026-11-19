namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The fixed shape of the World Cup finals: 32 nations in 8 groups of 4, each pair meeting once. The steps and the
/// helpers of the finals all read their numbers from here.
/// </summary>
internal static class WorldCupFinalsShape
{
    /// <summary>
    /// The number of nations that play the finals.
    /// </summary>
    internal const int NationCount = 32;

    /// <summary>
    /// The number of groups of the finals, which is also the number of pots of the draw.
    /// </summary>
    internal const int GroupCount = 8;

    /// <summary>
    /// The number of nations in a group of the finals.
    /// </summary>
    internal const int GroupSize = 4;

    /// <summary>
    /// The number of times each pair of a group meets: once.
    /// </summary>
    internal const int Legs = 1;

    /// <summary>
    /// The number of matches in one group: every pair of its nations plays once.
    /// </summary>
    internal const int MatchesPerGroup = 6;

    /// <summary>
    /// The number of group matches in the finals: 8 groups of 6.
    /// </summary>
    internal const int GroupMatchCount = GroupCount * MatchesPerGroup;
}
