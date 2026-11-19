using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Draws the groups of the World Cup qualifiers. Each confederation is drawn on its own, from pots made by strength, so
/// every group holds one nation of each pot. The draw is fixed by the season: the same entrants and season give the
/// same groups. It reads nothing and writes nothing.
/// </summary>
internal static class QualifierGroupDraw
{
    /// <summary>
    /// The start of the purpose a confederation's random source is derived with; the confederation's number follows it.
    /// </summary>
    private const string RandomPurposePrefix = "worldCupQualifierDraw:";

    /// <summary>
    /// The number of nations in a full group, and so the number of pots.
    /// </summary>
    private const int GroupSize = 4;

    /// <summary>
    /// The number of nations that play one group of their own, and play it as a single round robin.
    /// </summary>
    private const int SingleRoundRobinSize = 5;

    /// <summary>
    /// The fewest nations that play a qualifier. A confederation with one entrant gives it a direct place instead.
    /// </summary>
    private const int MinEntrants = 2;

    /// <summary>
    /// The number of legs of a group in which each pair meets at home and away.
    /// </summary>
    private const int DoubleLegs = 2;

    /// <summary>
    /// The number of legs of a group of <see cref="SingleRoundRobinSize"/> nations: each pair meets once.
    /// </summary>
    private const int SingleLeg = 1;

    /// <summary>
    /// Draws the groups of every confederation, the confederations in their order. A confederation with one entrant has
    /// no group. With 2 entrants it has one group of 2. With 5 it has one group of 5, played as a single round robin.
    /// Otherwise it has one group for every 4 entrants, rounded up, so its groups hold 4 or 3 nations. Pot k holds the
    /// nations at the strength places from k times the group count, one group count of them; the last pot may be short.
    /// Each pot is shuffled and its i-th nation goes to group i, so a short last pot fills the first groups.
    /// </summary>
    /// <param name="entrants">The nations that enter, with their confederations and strengths.</param>
    /// <param name="seasonId">The id of the season, which seeds the draw.</param>
    /// <returns>The groups, ordered by group number, which counts from 1 over the whole draw.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entrants"/> is null.</exception>
    /// <exception cref="OverflowException">Thrown when the draw has more than 255 groups.</exception>
    public static IReadOnlyList<QualifierGroup> Draw(IReadOnlyList<QualifierNation> entrants, int seasonId)
    {
        ArgumentNullException.ThrowIfNull(entrants);

        List<QualifierGroup> groups = [];

        IEnumerable<IGrouping<Confederation, QualifierNation>> confederations = entrants
            .GroupBy(entrant => entrant.Confederation)
            .OrderBy(confederation => confederation.Key);

        foreach (IGrouping<Confederation, QualifierNation> confederation in confederations)
        {
            // Sorted again, so the draw does not depend on the order the entrants arrived in.
            List<QualifierNation> ranked = confederation
                .OrderByDescending(entrant => entrant.Strength)
                .ThenBy(entrant => entrant.NationalTeamId)
                .ToList();

            groups.AddRange(DrawConfederation(ranked, confederation.Key, seasonId, groups.Count + 1));
        }

        return groups;
    }

    /// <summary>
    /// Draws the groups of one confederation.
    /// </summary>
    /// <param name="ranked">The confederation's entrants, the strongest first.</param>
    /// <param name="confederation">The confederation.</param>
    /// <param name="seasonId">The id of the season, which seeds the draw.</param>
    /// <param name="firstGroupNumber">The number of the confederation's first group.</param>
    /// <returns>The confederation's groups, in group order; none when it has a single entrant.</returns>
    /// <exception cref="OverflowException">Thrown when a group number does not fit in a byte.</exception>
    private static List<QualifierGroup> DrawConfederation(
        List<QualifierNation> ranked,
        Confederation confederation,
        int seasonId,
        int firstGroupNumber)
    {
        if (ranked.Count < MinEntrants)
        {
            return [];
        }

        if (ranked.Count == SingleRoundRobinSize)
        {
            return
            [
                new QualifierGroup(
                    checked((byte)firstGroupNumber),
                    confederation,
                    ranked.Select(entrant => entrant.LeagueTeamId).ToList(),
                    SingleLeg),
            ];
        }

        int groupCount = (ranked.Count + GroupSize - 1) / GroupSize;

        SeededRandom random = new SeededRandom(seasonId)
            .Derive(RandomPurposePrefix + ((int)confederation).ToString(CultureInfo.InvariantCulture));

        IReadOnlyList<IReadOnlyList<int>> members = PotDraw.Deal(
            ranked.Select(entrant => entrant.LeagueTeamId).ToList(),
            groupCount,
            random);

        List<QualifierGroup> groups = [];

        for (int group = 0; group < groupCount; group++)
        {
            groups.Add(new QualifierGroup(
                checked((byte)(firstGroupNumber + group)),
                confederation,
                members[group],
                DoubleLegs));
        }

        return groups;
    }
}
