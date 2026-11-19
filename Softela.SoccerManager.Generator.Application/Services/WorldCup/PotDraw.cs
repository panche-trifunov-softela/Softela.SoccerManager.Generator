using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Deals ranked nations into groups from pots, so every group holds one nation of each pot. The qualifier draw and the
/// finals draw both use it. It draws nothing but the shuffles of the pots, from the random source it is given.
/// </summary>
internal static class PotDraw
{
    /// <summary>
    /// Deals the nations into groups. Pot k holds the nations at the ranked places from k times the group count, one
    /// group count of them; the last pot may be short. The pots are taken in order, each is shuffled with
    /// <paramref name="random"/>, and its i-th nation goes to group i, so a short last pot fills the first groups.
    /// </summary>
    /// <param name="rankedIds">The ids of the nations, the strongest first.</param>
    /// <param name="groupCount">The number of groups, which is also the size of a full pot.</param>
    /// <param name="random">The random source; its state advances.</param>
    /// <returns>
    /// The members of each group, in pot order; there is one list for each group, and a group may be empty when there
    /// are fewer nations than groups.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rankedIds"/> or <paramref name="random"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="groupCount"/> is less than 1, or an id is listed more than once.
    /// </exception>
    internal static IReadOnlyList<IReadOnlyList<int>> Deal(
        IReadOnlyList<int> rankedIds,
        int groupCount,
        SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(rankedIds);
        ArgumentNullException.ThrowIfNull(random);

        if (groupCount < 1)
        {
            throw new ArgumentException("At least one group is needed.", nameof(groupCount));
        }

        if (rankedIds.Distinct().Count() != rankedIds.Count)
        {
            throw new ArgumentException("A nation is listed more than once.", nameof(rankedIds));
        }

        List<List<int>> members = Enumerable.Range(0, groupCount).Select(_ => new List<int>()).ToList();

        for (int start = 0; start < rankedIds.Count; start += groupCount)
        {
            List<int> pot = rankedIds.Skip(start).Take(groupCount).ToList();

            random.Shuffle(pot);

            for (int group = 0; group < pot.Count; group++)
            {
                members[group].Add(pot[group]);
            }
        }

        return members.Select(group => (IReadOnlyList<int>)group).ToList();
    }
}
