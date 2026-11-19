using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;
using Softela.SoccerManager.Generator.Application.Services.Ranking;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Picks the nations that qualify for the World Cup finals from the finished qualifier groups: the winners of the
/// groups of a confederation first, then its best runners-up, then its best third-placed nations, and so on, up to the
/// places the confederation has. It reads nothing and writes nothing.
/// </summary>
internal static class WorldCupQualification
{
    /// <summary>
    /// The number of nations in a group whose last-placed nation is left out of the comparison of its other nations.
    /// </summary>
    private const int ComparedGroupSize = 4;

    /// <summary>
    /// The start of the purpose the random source is derived with when nations are level; the ids of the nations
    /// that are level, in ascending order and separated by commas, follow it.
    /// </summary>
    private const string RandomPurposePrefix = "worldCupQualifierPlaces:";

    /// <summary>
    /// Picks the qualified nations. Each group is ranked in the World Cup table order. Within a confederation the
    /// nations are put in order by their place in their group, then by points, goal difference and goals scored, all
    /// highest first. The points and the goals are counted on comparable matches only: in a group of 4, a nation's
    /// matches without those against the group's last-placed nation; in any other group, all its matches. Nations that
    /// are still level are put in order by a random draw seeded by the season and the nations that are level. The first
    /// nations of each confederation, as many as it has places, qualify. A lone nation, which played no qualifier,
    /// qualifies directly.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the ranking and the draws.</param>
    /// <param name="groups">The finished qualifier groups.</param>
    /// <param name="lonelyNations">The roster entry of the lone nation of each confederation that has no group.</param>
    /// <param name="places">The number of places of each confederation, which has nations.</param>
    /// <returns>
    /// The qualified nations, ordered by confederation, then by place in the group, then in the order above; there are
    /// as many as the places add up to.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a confederation has more places than nations, a place count is negative or names a confederation
    /// with no nations, a confederation with nations has no place count, a lone nation is in a confederation that has a
    /// group, or the ranking refuses a group.
    /// </exception>
    internal static IReadOnlyList<QualifiedNation> Pick(
        int seasonId,
        IReadOnlyList<WrittenQualifierGroup> groups,
        IReadOnlyDictionary<Confederation, int> lonelyNations,
        IReadOnlyDictionary<Confederation, int> places)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(lonelyNations);
        ArgumentNullException.ThrowIfNull(places);

        List<Candidate> candidates = groups.SelectMany(group => RankGroup(seasonId, group)).ToList();

        foreach ((Confederation confederation, int leagueTeamId) in lonelyNations.OrderBy(pair => pair.Key))
        {
            if (candidates.Any(candidate => candidate.Confederation == confederation))
            {
                throw new ArgumentException(
                    $"{ConfederationLabel.Of(confederation)} has a qualifier group, so it has no lone nation.",
                    nameof(lonelyNations));
            }

            candidates.Add(new Candidate(leagueTeamId, confederation, 1, 0, 0, 0));
        }

        ILookup<Confederation, Candidate> byConfederation = candidates.ToLookup(candidate => candidate.Confederation);

        CheckPlaces(byConfederation, places);

        List<QualifiedNation> qualified = [];

        foreach ((Confederation confederation, int count) in places.OrderBy(pair => pair.Key))
        {
            List<Candidate> ordered = OrderConfederation(seasonId, byConfederation[confederation]);

            qualified.AddRange(ordered.Take(count).Select(candidate => new QualifiedNation(
                candidate.LeagueTeamId,
                candidate.Confederation)));
        }

        return qualified;
    }

    /// <summary>
    /// Checks that every confederation with nations has a place count, and that no place count is negative, names a
    /// confederation without nations or is more than the confederation's nations.
    /// </summary>
    /// <param name="byConfederation">The candidates of each confederation.</param>
    /// <param name="places">The number of places of each confederation.</param>
    /// <exception cref="ArgumentException">Thrown when the place counts do not fit the nations.</exception>
    private static void CheckPlaces(
        ILookup<Confederation, Candidate> byConfederation,
        IReadOnlyDictionary<Confederation, int> places)
    {
        foreach (IGrouping<Confederation, Candidate> confederation in byConfederation.OrderBy(group => group.Key))
        {
            if (!places.ContainsKey(confederation.Key))
            {
                throw new ArgumentException(
                    $"{ConfederationLabel.Of(confederation.Key)} has nations but no number of places.",
                    nameof(places));
            }
        }

        foreach ((Confederation confederation, int count) in places.OrderBy(pair => pair.Key))
        {
            int nations = byConfederation[confederation].Count();

            if (nations == 0)
            {
                throw new ArgumentException(
                    $"{ConfederationLabel.Of(confederation)} has places but no nations.",
                    nameof(places));
            }

            if (count < 0 || count > nations)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{ConfederationLabel.Of(confederation)} has {count} places and {nations} nations, so ")
                    + "its places do not fit its nations.",
                    nameof(places));
            }
        }
    }

    /// <summary>
    /// Ranks one group in the World Cup table order and works out each nation's comparable record.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the ranking.</param>
    /// <param name="group">The finished group.</param>
    /// <returns>One candidate for each nation of the group.</returns>
    /// <exception cref="ArgumentException">Thrown when the ranking refuses the group's matches.</exception>
    private static List<Candidate> RankGroup(int seasonId, WrittenQualifierGroup group)
    {
        List<TableMatch> tableMatches = SeasonTableMatches.Of(group.Matches);

        IReadOnlyList<TableRow> table = TableRanking.Rank(
            seasonId,
            group.LeagueTeamIds,
            tableMatches,
            TableOrder.WorldCup);

        int? discarded = group.LeagueTeamIds.Count == ComparedGroupSize
            ? table.Single(row => row.Place == ComparedGroupSize).Id
            : null;

        IReadOnlyDictionary<int, TableTotals> all = TableTally.Count(group.LeagueTeamIds, tableMatches);

        IReadOnlyDictionary<int, TableTotals> compared = discarded is { } last
            ? TableTally.Count(group.LeagueTeamIds.Where(id => id != last).ToList(), tableMatches)
            : all;

        List<Candidate> candidates = [];

        foreach (TableRow row in table)
        {
            TableTotals totals = discarded == row.Id ? all[row.Id] : compared[row.Id];

            candidates.Add(new Candidate(
                row.Id,
                group.Confederation,
                row.Place,
                totals.Points,
                totals.GoalDifference,
                totals.GoalsFor));
        }

        return candidates;
    }

    /// <summary>
    /// Puts the nations of one confederation in order: by place in the group, then by points, goal difference and goals
    /// scored, highest first; nations still level are put in order by a seeded draw.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the draws.</param>
    /// <param name="candidates">The confederation's nations.</param>
    /// <returns>The nations, the first to qualify first.</returns>
    private static List<Candidate> OrderConfederation(int seasonId, IEnumerable<Candidate> candidates)
    {
        List<Candidate> sorted = candidates
            .OrderBy(candidate => candidate.GroupPlace)
            .ThenByDescending(candidate => candidate.Points)
            .ThenByDescending(candidate => candidate.GoalDifference)
            .ThenByDescending(candidate => candidate.GoalsFor)
            .ThenBy(candidate => candidate.LeagueTeamId)
            .ToList();

        List<Candidate> ordered = [];
        int start = 0;

        while (start < sorted.Count)
        {
            int end = start + 1;

            while (end < sorted.Count && IsLevel(sorted[start], sorted[end]))
            {
                end++;
            }

            ordered.AddRange(DrawLevel(seasonId, sorted.GetRange(start, end - start)));

            start = end;
        }

        return ordered;
    }

    /// <summary>
    /// Tells whether two nations are level on every figure the order uses.
    /// </summary>
    /// <param name="first">The first nation.</param>
    /// <param name="second">The second nation.</param>
    /// <returns>True when the place, the points, the goal difference and the goals scored are all equal.</returns>
    private static bool IsLevel(Candidate first, Candidate second)
    {
        return first.GroupPlace == second.GroupPlace
            && first.Points == second.Points
            && first.GoalDifference == second.GoalDifference
            && first.GoalsFor == second.GoalsFor;
    }

    /// <summary>
    /// Puts nations that are level in order by a draw seeded by the season and the ids of the nations that are level.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the draw.</param>
    /// <param name="level">The nations that are level, in ascending order of roster entry.</param>
    /// <returns>The same nations; in the order of the draw when there are two or more.</returns>
    private static List<Candidate> DrawLevel(int seasonId, List<Candidate> level)
    {
        if (level.Count == 1)
        {
            return level;
        }

        List<int> ids = level.Select(candidate => candidate.LeagueTeamId).ToList();

        string purpose = RandomPurposePrefix + string.Join(
            ",",
            ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

        new SeededRandom(seasonId).Derive(purpose).Shuffle(ids);

        return ids.Select(id => level.Single(candidate => candidate.LeagueTeamId == id)).ToList();
    }

    /// <summary>
    /// A nation that may qualify, with the figures the order of a confederation uses.
    /// </summary>
    /// <param name="LeagueTeamId">The roster entry of the nation.</param>
    /// <param name="Confederation">The confederation of the nation.</param>
    /// <param name="GroupPlace">The nation's place in its group, 1 for a winner or a lone nation.</param>
    /// <param name="Points">The points over the compared matches.</param>
    /// <param name="GoalDifference">The goal difference over the compared matches.</param>
    /// <param name="GoalsFor">The goals scored over the compared matches.</param>
    private sealed record Candidate(
        int LeagueTeamId,
        Confederation Confederation,
        int GroupPlace,
        int Points,
        int GoalDifference,
        int GoalsFor);
}
