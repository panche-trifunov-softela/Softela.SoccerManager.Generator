using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// The pairs of clubs the league phase plays, drawn once: for each pot pair two matchings, and in a 32-club cup two
/// matchings inside each pot. The pot-pair matchings leave home and away open, as the flags of the arrangement decide
/// them. The inside matchings fix them by the direction of each cycle edge, the first club of an edge being at home,
/// which <see cref="ContinentalMatchdayRules.TypeIsHome"/> relies on.
/// </summary>
internal sealed class ContinentalPotMatchings
{
    /// <summary>
    /// The matchings of the pot pairs, two for each pair in pair order.
    /// </summary>
    private readonly (int Low, int High)[][] _potPairMatchings;

    /// <summary>
    /// The matchings inside each pot, two for each pot in pot order, or null for a 16-club cup.
    /// </summary>
    private readonly (int Home, int Away)[][]? _insideMatchings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinentalPotMatchings"/> class.
    /// </summary>
    /// <param name="potPairMatchings">The matchings of the pot pairs.</param>
    /// <param name="insideMatchings">The matchings inside each pot, or null for a 16-club cup.</param>
    private ContinentalPotMatchings(
        (int Low, int High)[][] potPairMatchings,
        (int Home, int Away)[][]? insideMatchings)
    {
        _potPairMatchings = potPairMatchings;
        _insideMatchings = insideMatchings;
    }

    /// <summary>
    /// Draws the matchings. The random calls come in a fixed order: first, for 32 clubs, the cycle of each pot in pot
    /// order, and then, for each pot pair in the order of <see cref="ContinentalMatchdayRules"/>, the shuffle of the
    /// higher pot and the shuffle of the lower pot.
    /// </summary>
    /// <param name="pots">The 4 pots, each sorted ascending; all four have the same size, 4 or 8.</param>
    /// <param name="random">The random source of the draw.</param>
    /// <returns>The drawn matchings.</returns>
    internal static ContinentalPotMatchings Draw(IReadOnlyList<List<int>> pots, SeededRandom random)
    {
        int clubCount = pots[0].Count * ContinentalMatchdayRules.PotCount;
        bool hasInsideMatchings = clubCount == ContinentalInputValidator.LargeCupClubCount;
        int slotCount = ContinentalMatchdayRules.PairCount * ContinentalMatchdayRules.MatchingCount;
        (int Home, int Away)[][]? insideMatchings = hasInsideMatchings ? DrawInsideMatchings(pots, random) : null;
        (int Low, int High)[][] potPairMatchings = new (int Low, int High)[slotCount][];

        for (int pairIndex = 0; pairIndex < ContinentalMatchdayRules.PairCount; pairIndex++)
        {
            (int low, int high) = ContinentalMatchdayRules.GetPair(pairIndex);

            int firstSlot = pairIndex * ContinentalMatchdayRules.MatchingCount;

            DrawPotPair(pots[low], pots[high], random, potPairMatchings, firstSlot);
        }

        return new ContinentalPotMatchings(potPairMatchings, insideMatchings);
    }

    /// <summary>
    /// Gets the pairs of one matching of a pot pair.
    /// </summary>
    /// <param name="pairIndex">The 0-based index of the pot pair.</param>
    /// <param name="matching">The matching, 0 or 1.</param>
    /// <returns>
    /// Every club of the lower pot with the club of the higher pot it meets, in the order of the lower pot.
    /// </returns>
    internal IReadOnlyList<(int Low, int High)> PotPair(int pairIndex, int matching)
    {
        return _potPairMatchings[(pairIndex * ContinentalMatchdayRules.MatchingCount) + matching];
    }

    /// <summary>
    /// Gets the pairs of one matching inside a pot.
    /// </summary>
    /// <param name="pot">The 0-based index of the pot.</param>
    /// <param name="matching">The matching, 0 or 1.</param>
    /// <returns>The home club and the away club of every match, in cycle order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the cup has 16 clubs, which have no matching inside a pot; this is a bug.
    /// </exception>
    internal IReadOnlyList<(int Home, int Away)> Inside(int pot, int matching)
    {
        if (_insideMatchings is null)
        {
            throw new InvalidOperationException("A 16-club cup has no matching inside a pot.");
        }

        return _insideMatchings[(pot * ContinentalMatchdayRules.MatchingCount) + matching];
    }

    /// <summary>
    /// Draws the cycle of every pot and cuts it into the two matchings inside the pot. The cycle joins each club to the
    /// next one, and the last club to the first; the club of an edge that comes first is at home.
    /// </summary>
    /// <param name="pots">The 4 pots, each sorted ascending.</param>
    /// <param name="random">The random source of the draw.</param>
    /// <returns>For each pot, the edges at even places and then the edges at odd places.</returns>
    private static (int Home, int Away)[][] DrawInsideMatchings(IReadOnlyList<List<int>> pots, SeededRandom random)
    {
        List<List<int>> cycles = new();

        foreach (List<int> pot in pots)
        {
            List<int> cycle = new(pot);

            random.Shuffle(cycle);
            cycles.Add(cycle);
        }

        int slotCount = ContinentalMatchdayRules.PotCount * ContinentalMatchdayRules.MatchingCount;
        (int Home, int Away)[][] matchings = new (int Home, int Away)[slotCount][];

        for (int pot = 0; pot < cycles.Count; pot++)
        {
            List<int> cycle = cycles[pot];
            List<(int Home, int Away)> even = new();
            List<(int Home, int Away)> odd = new();

            for (int edge = 0; edge < cycle.Count; edge++)
            {
                (int, int) match = (cycle[edge], cycle[(edge + 1) % cycle.Count]);

                (edge % ContinentalMatchdayRules.MatchingCount == 0 ? even : odd).Add(match);
            }

            matchings[pot * ContinentalMatchdayRules.MatchingCount] = even.ToArray();
            matchings[(pot * ContinentalMatchdayRules.MatchingCount) + 1] = odd.ToArray();
        }

        return matchings;
    }

    /// <summary>
    /// Draws the two matchings of one pot pair. The first meets every club of the lower pot with a club of the higher
    /// pot, by a shuffled pairing. The second gives each club of the lower pot the club of the higher pot that the next
    /// club of a shuffled cycle of the lower pot meets in the first matching, so that no club meets the same opponent
    /// twice.
    /// </summary>
    /// <param name="lowPot">The lower pot, sorted ascending.</param>
    /// <param name="highPot">The higher pot, sorted ascending.</param>
    /// <param name="random">The random source of the draw.</param>
    /// <param name="matchings">The array the two matchings are stored in.</param>
    /// <param name="firstSlot">The index in <paramref name="matchings"/> of the first matching.</param>
    private static void DrawPotPair(
        List<int> lowPot,
        List<int> highPot,
        SeededRandom random,
        (int Low, int High)[][] matchings,
        int firstSlot)
    {
        List<int> partners = new(highPot);

        random.Shuffle(partners);

        List<int> cycle = new(lowPot);

        random.Shuffle(cycle);

        Dictionary<int, int> next = new();
        Dictionary<int, int> placeInLowPot = new();

        for (int place = 0; place < cycle.Count; place++)
        {
            next[cycle[place]] = cycle[(place + 1) % cycle.Count];
            placeInLowPot[lowPot[place]] = place;
        }

        (int Low, int High)[] first = new (int Low, int High)[lowPot.Count];
        (int Low, int High)[] second = new (int Low, int High)[lowPot.Count];

        for (int place = 0; place < lowPot.Count; place++)
        {
            int club = lowPot[place];

            first[place] = (club, partners[place]);
            second[place] = (club, partners[placeInLowPot[next[club]]]);
        }

        matchings[firstSlot] = first;
        matchings[firstSlot + 1] = second;
    }
}
