using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Draws the league phase of a continental cup: who plays whom, at home or away, on which matchday. The entrants are
/// cut into 4 pots by their order. In a 16-club cup each club plays two clubs of every other pot; in a 32-club cup it
/// plays two clubs of every pot, its own included. Every matchday every club plays once. No club plays more than two
/// home or two away matches in a row, and every club plays one home and one away match in the first two matchdays and
/// in the last two.
/// </summary>
public static class ContinentalLeaguePhaseDraw
{
    /// <summary>
    /// The start of the purpose a cup's random source is derived with; the cup's id follows it.
    /// </summary>
    private const string RandomPurposePrefix = "continentalLeaguePhase:";

    /// <summary>
    /// Creates the random source of one cup's league-phase draw. Every call gives a fresh source, so the same season
    /// and cup always give the same draw, and the two cups of one season get different sources.
    /// </summary>
    /// <param name="seasonId">The id of the season.</param>
    /// <param name="competitionId">The id of the continental cup.</param>
    /// <returns>A new random source for the draw.</returns>
    public static SeededRandom CreateRandom(int seasonId, int competitionId)
    {
        string purpose = RandomPurposePrefix + competitionId.ToString(CultureInfo.InvariantCulture);

        return new SeededRandom(seasonId).Derive(purpose);
    }

    /// <summary>
    /// Draws the league phase. The result is the same for the same entrants and the same random source state.
    /// </summary>
    /// <param name="entrants">
    /// The league team ids of the 16 or 32 clubs, in pot order: the first quarter is pot 1, the second pot 2, and so
    /// on.
    /// </param>
    /// <param name="random">The random source of the draw, as <see cref="CreateRandom"/> makes it.</param>
    /// <returns>
    /// Every league-phase match, sorted by matchday and then by the home club's league team id: 48 matches for 16 clubs
    /// over 6 matchdays, and 128 for 32 clubs over 8 matchdays.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entrants"/> or <paramref name="random"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are not 16 or 32 entrants, or a club is listed more than once.
    /// </exception>
    public static IReadOnlyList<ContinentalFixture> Draw(IReadOnlyList<int> entrants, SeededRandom random)
    {
        ContinentalInputValidator.ValidateClubs(entrants, nameof(entrants));
        ArgumentNullException.ThrowIfNull(random);

        int clubCount = entrants.Count;
        List<List<int>> pots = CutPots(entrants);
        ContinentalPotMatchings matchings = ContinentalPotMatchings.Draw(pots, random);
        (int flags, int[] order) = ContinentalMatchdayOrder.Pick(clubCount, random);

        List<ContinentalFixture> fixtures = new();

        for (int day = 0; day < order.Length; day++)
        {
            fixtures.AddRange(MatchdayFixtures(day + 1, order[day], flags, matchings));
        }

        return fixtures
            .OrderBy(fixture => fixture.Matchday)
            .ThenBy(fixture => fixture.HomeLeagueTeamId)
            .ToList();
    }

    /// <summary>
    /// Cuts the entrants into 4 pots of equal size.
    /// </summary>
    /// <param name="entrants">The league team ids, in pot order.</param>
    /// <returns>The 4 pots, each sorted ascending.</returns>
    private static List<List<int>> CutPots(IReadOnlyList<int> entrants)
    {
        int potSize = entrants.Count / ContinentalMatchdayRules.PotCount;
        List<List<int>> pots = new();

        for (int pot = 0; pot < ContinentalMatchdayRules.PotCount; pot++)
        {
            List<int> clubs = entrants.Skip(pot * potSize).Take(potSize).ToList();

            clubs.Sort();
            pots.Add(clubs);
        }

        return pots;
    }

    /// <summary>
    /// Makes the matches of one matchday from its content.
    /// </summary>
    /// <param name="matchday">The 1-based matchday.</param>
    /// <param name="contentId">The content id of the matchday.</param>
    /// <param name="flags">The flags of the pot pairs.</param>
    /// <param name="matchings">The drawn matchings.</param>
    /// <returns>The matches of the matchday, with their home and away clubs.</returns>
    private static List<ContinentalFixture> MatchdayFixtures(
        int matchday,
        int contentId,
        int flags,
        ContinentalPotMatchings matchings)
    {
        List<ContinentalFixture> fixtures = new();

        if (contentId >= ContinentalMatchdayRules.PotAgainstPotIdCount)
        {
            int inside = contentId - ContinentalMatchdayRules.PotAgainstPotIdCount;

            for (int pot = 0; pot < ContinentalMatchdayRules.PotCount; pot++)
            {
                foreach ((int home, int away) in matchings.Inside(pot, inside))
                {
                    fixtures.Add(new ContinentalFixture(matchday, home, away));
                }
            }

            return fixtures;
        }

        int pairing = contentId / ContinentalMatchdayRules.MatchingCount;
        int matching = contentId % ContinentalMatchdayRules.MatchingCount;

        foreach (int pairIndex in ContinentalMatchdayRules.PairIndexesOf(pairing))
        {
            bool lowerPotHome = ContinentalMatchdayRules.LowerPotHome(flags, pairIndex, matching);

            foreach ((int low, int high) in matchings.PotPair(pairIndex, matching))
            {
                fixtures.Add(lowerPotHome
                    ? new ContinentalFixture(matchday, low, high)
                    : new ContinentalFixture(matchday, high, low));
            }
        }

        return fixtures;
    }
}
