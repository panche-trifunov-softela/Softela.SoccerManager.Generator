using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Holds every allowed arrangement of the league phase and picks one. An arrangement is the flags of the pot pairs
/// (which pot of each pair is at home first) and the order in which the matchday contents are played. The lists are
/// built the first time a size is used and are then shared.
/// </summary>
internal static class ContinentalMatchdayOrder
{
    /// <summary>
    /// The number of bits that hold the flags of the 6 pot pairs in a packed arrangement.
    /// </summary>
    private const int FlagBits = 6;

    /// <summary>
    /// The number of different flag values, which is 2 to the power of the number of flag bits.
    /// </summary>
    private const int FlagCount = 1 << FlagBits;

    /// <summary>
    /// The number of bits that hold the content id of one matchday in a packed arrangement.
    /// </summary>
    private const int OrderDigitBits = 3;

    /// <summary>
    /// The mask that keeps the bits of one matchday's content id.
    /// </summary>
    private const int OrderDigitMask = (1 << OrderDigitBits) - 1;

    /// <summary>
    /// The allowed arrangements of a 16-club cup, built the first time they are needed.
    /// </summary>
    private static readonly Lazy<int[]> SmallCupArrangements = new(
        () => Enumerate(ContinentalInputValidator.SmallCupClubCount),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The allowed arrangements of a 32-club cup, built the first time they are needed.
    /// </summary>
    private static readonly Lazy<int[]> LargeCupArrangements = new(
        () => Enumerate(ContinentalInputValidator.LargeCupClubCount),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Picks one allowed arrangement with a single call to the random source.
    /// </summary>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <param name="random">The random source of the draw.</param>
    /// <returns>
    /// The flags of the pot pairs, and the content id of every matchday in matchday order (the first item is
    /// matchday 1).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the club count is not 16 or 32.</exception>
    internal static (int Flags, int[] Order) Pick(int clubCount, SeededRandom random)
    {
        int[] arrangements = ArrangementsFor(clubCount);
        int packed = arrangements[random.NextInt(arrangements.Length)];

        return Unpack(packed, ContinentalMatchdayRules.MatchdayCount(clubCount));
    }

    /// <summary>
    /// Gets the list of allowed arrangements of a cup size, building it on first use.
    /// </summary>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <returns>The packed arrangements, in the order the enumeration found them.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the club count is not 16 or 32.</exception>
    private static int[] ArrangementsFor(int clubCount)
    {
        return clubCount switch
        {
            ContinentalInputValidator.SmallCupClubCount => SmallCupArrangements.Value,
            ContinentalInputValidator.LargeCupClubCount => LargeCupArrangements.Value,
            _ => throw new ArgumentOutOfRangeException(
                nameof(clubCount),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A continental cup has no arrangements for {clubCount} clubs.")),
        };
    }

    /// <summary>
    /// Finds every allowed arrangement of a cup size: the flags from 0 up, and for each the orders of the content ids
    /// in lexicographic order, keeping those in which every club type's home and away sequence is allowed.
    /// </summary>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <returns>
    /// The packed arrangements. A packed arrangement holds the flags in its lowest 6 bits and the content id of
    /// matchday d in the 3 bits that start at bit 6 + 3 * (d - 1).
    /// </returns>
    private static int[] Enumerate(int clubCount)
    {
        int matchdayCount = ContinentalMatchdayRules.MatchdayCount(clubCount);
        int typeCount = ContinentalMatchdayRules.TypeCount(clubCount);
        List<int> found = new();

        for (int flags = 0; flags < FlagCount; flags++)
        {
            bool[,] isHome = HomeTable(flags, clubCount, typeCount, matchdayCount);
            int[] order = new int[matchdayCount];
            bool[] used = new bool[matchdayCount];
            bool[][] sequences = new bool[typeCount][];

            for (int type = 0; type < typeCount; type++)
            {
                sequences[type] = new bool[matchdayCount];
            }

            Search(flags, isHome, sequences, order, used, 0, found);
        }

        return found.ToArray();
    }

    /// <summary>
    /// Fills the table that says whether each club type is at home on a matchday with each content id.
    /// </summary>
    /// <param name="flags">The flags of the pot pairs, 0 to 63.</param>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <param name="typeCount">The number of club types.</param>
    /// <param name="matchdayCount">The number of matchdays, which is also the number of content ids.</param>
    /// <returns>A table indexed by club type and then by content id; true is a home match.</returns>
    private static bool[,] HomeTable(int flags, int clubCount, int typeCount, int matchdayCount)
    {
        bool[,] isHome = new bool[typeCount, matchdayCount];

        for (int type = 0; type < typeCount; type++)
        {
            for (int contentId = 0; contentId < matchdayCount; contentId++)
            {
                isHome[type, contentId] = ContinentalMatchdayRules.TypeIsHome(flags, clubCount, type, contentId);
            }
        }

        return isHome;
    }

    /// <summary>
    /// Places a content id on each matchday from <paramref name="depth"/> on, in every way that keeps the home and
    /// away sequence of every club type allowed, and records each complete order as a packed arrangement.
    /// </summary>
    /// <param name="flags">The flags of the pot pairs, 0 to 63.</param>
    /// <param name="isHome">The table that <see cref="HomeTable"/> made.</param>
    /// <param name="sequences">The home and away sequence of every club type so far; this method changes it.</param>
    /// <param name="order">The content id of every matchday placed so far; this method changes it.</param>
    /// <param name="used">Which content ids are already placed; this method changes it.</param>
    /// <param name="depth">The 0-based matchday that gets its content id next.</param>
    /// <param name="found">The packed arrangements found so far; this method adds to it.</param>
    private static void Search(
        int flags,
        bool[,] isHome,
        bool[][] sequences,
        int[] order,
        bool[] used,
        int depth,
        List<int> found)
    {
        int matchdayCount = order.Length;
        int typeCount = sequences.Length;

        if (depth == matchdayCount)
        {
            found.Add(Pack(flags, order));

            return;
        }

        for (int contentId = 0; contentId < matchdayCount; contentId++)
        {
            if (used[contentId])
            {
                continue;
            }

            bool allowed = true;

            for (int type = 0; type < typeCount; type++)
            {
                sequences[type][depth] = isHome[type, contentId];

                if (!ContinentalMatchdayRules.PrefixAllowed(sequences[type], depth + 1, matchdayCount))
                {
                    allowed = false;

                    break;
                }
            }

            if (!allowed)
            {
                continue;
            }

            used[contentId] = true;
            order[depth] = contentId;

            Search(flags, isHome, sequences, order, used, depth + 1, found);

            used[contentId] = false;
        }
    }

    /// <summary>
    /// Packs an arrangement into one integer.
    /// </summary>
    /// <param name="flags">The flags of the pot pairs, 0 to 63.</param>
    /// <param name="order">The content id of every matchday.</param>
    /// <returns>The flags in the lowest 6 bits, and above them the content id of each matchday in 3 bits.</returns>
    private static int Pack(int flags, int[] order)
    {
        int packed = flags;

        for (int day = 0; day < order.Length; day++)
        {
            packed |= order[day] << (FlagBits + (OrderDigitBits * day));
        }

        return packed;
    }

    /// <summary>
    /// Unpacks an arrangement that <see cref="Pack"/> made.
    /// </summary>
    /// <param name="packed">The packed arrangement.</param>
    /// <param name="matchdayCount">The number of matchdays.</param>
    /// <returns>The flags and the content id of every matchday.</returns>
    private static (int Flags, int[] Order) Unpack(int packed, int matchdayCount)
    {
        int[] order = new int[matchdayCount];

        for (int day = 0; day < matchdayCount; day++)
        {
            order[day] = (packed >> (FlagBits + (OrderDigitBits * day))) & OrderDigitMask;
        }

        return (packed & (FlagCount - 1), order);
    }
}
