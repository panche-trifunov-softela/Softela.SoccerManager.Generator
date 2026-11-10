namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// Refuses round robin input that cannot be scheduled.
/// </summary>
internal static class RoundRobinInputValidator
{
    private const int MinGroupSize = 2;

    /// <summary>
    /// Checks the groups and the shared-stadium pairs.
    /// </summary>
    /// <param name="groups">The clubs of each group, by club id.</param>
    /// <param name="sharedStadiums">The pairs of clubs that share a stadium.</param>
    /// <returns>The index of the group each club belongs to, by club id.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>there are no groups, or a group has fewer than 2 clubs;</description></item>
    /// <item><description>a club appears twice, inside a group or across groups;</description></item>
    /// <item><description>a pair names the same club twice, or a club that is in no group;</description></item>
    /// <item><description>a club is in more than one pair;</description></item>
    /// <item><description>a pair crosses groups of different sizes;</description></item>
    /// <item><description>more than half of a group's clubs, rounded down, are in pairs.</description></item>
    /// </list>
    /// </exception>
    public static IReadOnlyDictionary<int, int> Validate(
        IReadOnlyList<IReadOnlyList<int>> groups,
        IReadOnlyList<SharedStadiumPair> sharedStadiums)
    {
        Dictionary<int, int> groupOfClub = ValidateGroups(groups);

        ValidatePairs(groups, sharedStadiums, groupOfClub);
        ValidatePairLimit(groups, sharedStadiums, groupOfClub);

        return groupOfClub;
    }

    /// <summary>
    /// Gets the most clubs of a group that may be in shared-stadium pairs: half the group, rounded down. This limit is
    /// what guarantees ClubPlacement a free position for every pair, for every seed, and the fixture generator refuses
    /// a league over it before scheduling.
    /// </summary>
    /// <param name="groupSize">The number of clubs in the group.</param>
    /// <returns>The most clubs of the group that may be in pairs.</returns>
    internal static int MaxPairedClubs(int groupSize)
    {
        return groupSize / 2;
    }

    /// <summary>
    /// Checks the groups and maps each club to its group.
    /// </summary>
    /// <param name="groups">The clubs of each group.</param>
    /// <returns>The index of the group each club belongs to.</returns>
    /// <exception cref="ArgumentException">Thrown when a group rule is broken.</exception>
    private static Dictionary<int, int> ValidateGroups(IReadOnlyList<IReadOnlyList<int>> groups)
    {
        if (groups.Count == 0)
        {
            throw new ArgumentException("At least one group is required.", nameof(groups));
        }

        Dictionary<int, int> groupOfClub = new();

        for (int group = 0; group < groups.Count; group++)
        {
            if (groups[group].Count < MinGroupSize)
            {
                throw new ArgumentException(
                    $"Group {group} has {groups[group].Count} club(s); a group needs at least {MinGroupSize}.",
                    nameof(groups));
            }

            foreach (int clubId in groups[group])
            {
                if (!groupOfClub.TryAdd(clubId, group))
                {
                    throw new ArgumentException($"Club {clubId} appears more than once in the groups.", nameof(groups));
                }
            }
        }

        return groupOfClub;
    }

    /// <summary>
    /// Checks each pair on its own and against the other pairs.
    /// </summary>
    /// <param name="groups">The clubs of each group.</param>
    /// <param name="sharedStadiums">The pairs of clubs that share a stadium.</param>
    /// <param name="groupOfClub">The index of the group each club belongs to.</param>
    /// <exception cref="ArgumentException">Thrown when a pair rule is broken.</exception>
    private static void ValidatePairs(
        IReadOnlyList<IReadOnlyList<int>> groups,
        IReadOnlyList<SharedStadiumPair> sharedStadiums,
        Dictionary<int, int> groupOfClub)
    {
        HashSet<int> pairedClubs = new();

        foreach (SharedStadiumPair pair in sharedStadiums)
        {
            if (pair.FirstClubId == pair.SecondClubId)
            {
                throw new ArgumentException(
                    $"A shared-stadium pair names club {pair.FirstClubId} twice.",
                    nameof(sharedStadiums));
            }

            foreach (int clubId in new[] { pair.FirstClubId, pair.SecondClubId })
            {
                if (!groupOfClub.ContainsKey(clubId))
                {
                    throw new ArgumentException(
                        $"Club {clubId} is in a shared-stadium pair but in no group.",
                        nameof(sharedStadiums));
                }

                if (!pairedClubs.Add(clubId))
                {
                    throw new ArgumentException(
                        $"Club {clubId} is in more than one shared-stadium pair.",
                        nameof(sharedStadiums));
                }
            }

            int firstSize = groups[groupOfClub[pair.FirstClubId]].Count;
            int secondSize = groups[groupOfClub[pair.SecondClubId]].Count;

            if (firstSize != secondSize)
            {
                throw new ArgumentException(
                    $"Clubs {pair.FirstClubId} and {pair.SecondClubId} share a stadium but their groups have "
                    + $"different sizes ({firstSize} and {secondSize}), so their rounds do not line up.",
                    nameof(sharedStadiums));
            }
        }
    }

    /// <summary>
    /// Checks that no group has more than half of its clubs in pairs.
    /// </summary>
    /// <param name="groups">The clubs of each group.</param>
    /// <param name="sharedStadiums">The pairs of clubs that share a stadium.</param>
    /// <param name="groupOfClub">The index of the group each club belongs to.</param>
    /// <exception cref="ArgumentException">Thrown when a group is over the limit.</exception>
    private static void ValidatePairLimit(
        IReadOnlyList<IReadOnlyList<int>> groups,
        IReadOnlyList<SharedStadiumPair> sharedStadiums,
        Dictionary<int, int> groupOfClub)
    {
        int[] pairedPerGroup = new int[groups.Count];

        foreach (SharedStadiumPair pair in sharedStadiums)
        {
            pairedPerGroup[groupOfClub[pair.FirstClubId]]++;
            pairedPerGroup[groupOfClub[pair.SecondClubId]]++;
        }

        for (int group = 0; group < groups.Count; group++)
        {
            int limit = MaxPairedClubs(groups[group].Count);

            if (pairedPerGroup[group] > limit)
            {
                throw new ArgumentException(
                    $"Group {group} has {pairedPerGroup[group]} clubs in shared-stadium pairs; the limit is {limit} "
                    + $"for a group of {groups[group].Count}.",
                    nameof(sharedStadiums));
            }
        }
    }
}
