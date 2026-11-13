namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// The rules of the league phase: which pots meet, who is at home, and which orders of the matchdays are allowed. The
/// league phase has 4 pots. Every pair of pots is met by two matchings (a matching pairs each club of the lower pot
/// with a club of the higher pot), and the 6 pot pairs are grouped into 3 pairings in which every pot appears once.
/// A matchday plays one content id: ids 0 to 5 are the pairing and its matching (id = 2 * pairing + matching), and in
/// a 32-club cup ids 6 and 7 are the matching inside each pot.
/// </summary>
internal static class ContinentalMatchdayRules
{
    /// <summary>
    /// The number of pots.
    /// </summary>
    internal const int PotCount = 4;

    /// <summary>
    /// The number of pot pairs.
    /// </summary>
    internal const int PairCount = 6;

    /// <summary>
    /// The number of content ids that match pot against pot: 3 pairings of 2 matchings each.
    /// </summary>
    internal const int PotAgainstPotIdCount = 6;

    /// <summary>
    /// The number of matchings of every pot pair, and of every pot's inside matching.
    /// </summary>
    internal const int MatchingCount = 2;

    /// <summary>
    /// The number of club types a pot has in a 32-club cup: the clubs at even and at odd positions of its cycle.
    /// </summary>
    private const int LargeCupParityCount = 2;

    /// <summary>
    /// The pot pairs, as the lower pot and the higher pot, in the order the draw uses them.
    /// </summary>
    private static readonly (int Low, int High)[] Pairs = { (0, 1), (0, 2), (0, 3), (1, 2), (1, 3), (2, 3) };

    /// <summary>
    /// Gets the lower and the higher pot of a pot pair.
    /// </summary>
    /// <param name="pairIndex">The 0-based index of the pot pair.</param>
    /// <returns>The 0-based indexes of the lower pot and the higher pot.</returns>
    internal static (int Low, int High) GetPair(int pairIndex)
    {
        return Pairs[pairIndex];
    }

    /// <summary>
    /// Gets the pairing a pot pair belongs to. In a pairing every pot meets exactly one other pot.
    /// </summary>
    /// <param name="pairIndex">The 0-based index of the pot pair.</param>
    /// <returns>
    /// The pairing: 0 for the pots (0, 1) and (2, 3), 1 for (0, 2) and (1, 3), and 2 for (0, 3) and (1, 2).
    /// </returns>
    internal static int PairingOf(int pairIndex)
    {
        (int low, int high) = Pairs[pairIndex];

        return (low ^ high) - 1;
    }

    /// <summary>
    /// Gets the pot pairs of a pairing, in the order the draw uses them.
    /// </summary>
    /// <param name="pairing">The pairing, 0 to 2.</param>
    /// <returns>The 0-based indexes of the two pot pairs.</returns>
    internal static IReadOnlyList<int> PairIndexesOf(int pairing)
    {
        List<int> indexes = new();

        for (int index = 0; index < PairCount; index++)
        {
            if (PairingOf(index) == pairing)
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    /// <summary>
    /// Gets the number of matchdays of a cup, which is also its number of content ids. The counts must equal
    /// <see cref="Calendar.CalendarTemplate.SmallFormatLeaguePhaseDateCount"/> and
    /// <see cref="Calendar.CalendarTemplate.LargeFormatLeaguePhaseDateCount"/>, because matchday N is played on the
    /// Nth league-phase date.
    /// </summary>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <returns>6 for 16 clubs and 8 for 32 clubs.</returns>
    internal static int MatchdayCount(int clubCount)
    {
        return clubCount == ContinentalInputValidator.LargeCupClubCount
            ? PotAgainstPotIdCount + MatchingCount
            : PotAgainstPotIdCount;
    }

    /// <summary>
    /// Says whether the lower pot of a pot pair is at home in a matching.
    /// </summary>
    /// <param name="flags">The arrangement's flags; bit i belongs to the pot pair with index i.</param>
    /// <param name="pairIndex">The 0-based index of the pot pair.</param>
    /// <param name="matching">The matching, 0 or 1.</param>
    /// <returns>
    /// True when the club of the lower pot is at home. With the pair's flag set the lower pot is at home in matching 0
    /// and away in matching 1; with it clear it is the other way round.
    /// </returns>
    internal static bool LowerPotHome(int flags, int pairIndex, int matching)
    {
        bool flagSet = ((flags >> pairIndex) & 1) == 1;

        return flagSet == (matching == 0);
    }

    /// <summary>
    /// Gets the number of club types of a cup. Clubs of a type share one sequence of home and away matches: all clubs
    /// of a pot in a 16-club cup, and the even-placed and the odd-placed clubs of a pot in a 32-club cup.
    /// </summary>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <returns>4 for 16 clubs and 8 for 32 clubs.</returns>
    internal static int TypeCount(int clubCount)
    {
        return PotCount * ParityCount(clubCount);
    }

    /// <summary>
    /// Says whether a club type is at home on a matchday whose content is the given id. The parity rule must agree
    /// with <see cref="ContinentalPotMatchings"/>, where the home club of inside edge k is the club at cycle
    /// position k.
    /// </summary>
    /// <param name="flags">The arrangement's flags.</param>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <param name="typeIndex">
    /// The club type: pot times the number of parities, plus the parity (the club's position in its pot's cycle, modulo
    /// 2; always 0 in a 16-club cup).
    /// </param>
    /// <param name="contentId">The content id of the matchday.</param>
    /// <returns>
    /// True when the type is at home. For ids 0 to 5: when its pot is the lower pot of the pair and the lower pot is
    /// at home, or its pot is the higher pot and the lower pot is away. For id 6 the clubs at an even position of
    /// their pot's cycle are at home, and for id 7 those at an odd position.
    /// </returns>
    internal static bool TypeIsHome(int flags, int clubCount, int typeIndex, int contentId)
    {
        int parityCount = ParityCount(clubCount);
        int pot = typeIndex / parityCount;
        int parity = typeIndex % parityCount;

        if (contentId >= PotAgainstPotIdCount)
        {
            return parity == contentId - PotAgainstPotIdCount;
        }

        int pairing = contentId / MatchingCount;
        int matching = contentId % MatchingCount;
        int partner = pot ^ (pairing + 1);
        int pairIndex = PairIndexOf(Math.Min(pot, partner), Math.Max(pot, partner));

        return (pot < partner) == LowerPotHome(flags, pairIndex, matching);
    }

    /// <summary>
    /// Says whether a club type's home and away sequence so far breaks no rule. The home and away counts are taken
    /// over the whole prefix; the run rule and the first-two and last-two rules look at the end of the prefix. The
    /// caller checks every prefix as it grows, so a full sequence that passes keeps every rule. The rules are: no more
    /// than half of the matchdays at home, and no more than half away; no three matchdays in a row at home or in a row
    /// away; one home and one away match in the first two matchdays; and one home and one away match in the last two
    /// matchdays.
    /// </summary>
    /// <param name="sequence">The sequence by matchday; true is a home match.</param>
    /// <param name="length">The number of matchdays set; it is at least 1.</param>
    /// <param name="matchdayCount">The number of matchdays of the whole cup.</param>
    /// <returns>True when the prefix can still become an allowed sequence.</returns>
    internal static bool PrefixAllowed(bool[] sequence, int length, int matchdayCount)
    {
        int homeCount = 0;

        for (int day = 0; day < length; day++)
        {
            if (sequence[day])
            {
                homeCount++;
            }
        }

        int half = matchdayCount / 2;

        if (homeCount > half || length - homeCount > half)
        {
            return false;
        }

        bool last = sequence[length - 1];

        if (length >= 3 && sequence[length - 2] == last && sequence[length - 3] == last)
        {
            return false;
        }

        if (length == 2 && sequence[0] == sequence[1])
        {
            return false;
        }

        return length != matchdayCount || sequence[length - 2] != last;
    }

    /// <summary>
    /// Gets the index of a pot pair.
    /// </summary>
    /// <param name="low">The lower pot.</param>
    /// <param name="high">The higher pot.</param>
    /// <returns>The 0-based index of the pot pair.</returns>
    /// <exception cref="ArgumentException">Thrown when the two pots are not a pot pair, which is a bug.</exception>
    private static int PairIndexOf(int low, int high)
    {
        for (int index = 0; index < PairCount; index++)
        {
            if (Pairs[index].Low == low && Pairs[index].High == high)
            {
                return index;
            }
        }

        throw new ArgumentException("The pots are not a pot pair.", nameof(low));
    }

    /// <summary>
    /// Gets the number of club types a pot has.
    /// </summary>
    /// <param name="clubCount">The number of clubs, 16 or 32.</param>
    /// <returns>1 for 16 clubs and 2 for 32 clubs.</returns>
    private static int ParityCount(int clubCount)
    {
        return clubCount == ContinentalInputValidator.LargeCupClubCount ? LargeCupParityCount : 1;
    }
}
