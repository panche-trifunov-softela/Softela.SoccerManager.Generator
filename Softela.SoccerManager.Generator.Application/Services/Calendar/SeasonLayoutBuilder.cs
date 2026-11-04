using System.Numerics;

namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// Lays out the weeks of a season from the shape of its league.
/// </summary>
internal static class SeasonLayoutBuilder
{
    private const int FirstGameWeek = 1;
    private const int FirstGap = 0;
    private const int LegsPerTie = 2;

    /// <summary>
    /// Builds the layout of a season in 1-based game weeks. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// The league has one week per round of a double round robin, from game week 1 to the last game week.
    /// </description>
    /// </item>
    /// <item><description>The continental final is in the last game week.</description></item>
    /// <item>
    /// <description>
    /// Apart from the wait of a top-8 club, which skips the play-off, no club waits more than 2 game weeks (8 days)
    /// between matches.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// When the league rounds and the windows need more game weeks than fit around the continental dates, the
    /// continental cups start later.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// When the domestic cup rounds and the continental dates need more game weeks than the league has rounds, the
    /// league skips some game weeks outside the windows.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="clubsPerDivision">The number of clubs in each division; it must already be validated.</param>
    /// <param name="divisionCount">The number of divisions; it must already be validated.</param>
    /// <returns>The weeks of the season's league, domestic cup, international windows and continental cups.</returns>
    public static SeasonLayout Build(int clubsPerDivision, int divisionCount)
    {
        int leagueRounds = (clubsPerDivision - 1) * CalendarTemplate.LeagueLegs;
        int cupRounds = DomesticCupRoundCount(clubsPerDivision * divisionCount);
        int windowCount = CalendarTemplate.WindowGaps.Count;

        // A club week has one league slot and one midweek slot, so there are as many club weeks as the longer of the
        // league rounds and the midweek dates (cup rounds and continental dates). The windows come on top.
        int gameWeekCount = Math.Max(leagueRounds, cupRounds + CalendarTemplate.ContinentalDateCount) + windowCount;

        // A free week is a game week with no continental date: a window, a domestic cup round, a rest week in a gap (a
        // league round and no midweek match), or a week before the first continental date.
        int freeWeekCount = gameWeekCount - CalendarTemplate.ContinentalDateCount;

        // A gap holds at most one free week, and only the window gaps and the open gaps can hold one. The free weeks
        // that do not fit come before the first continental date, so the continental cups start later.
        int freeWeeksInGaps = Math.Min(freeWeekCount, windowCount + CalendarTemplate.OpenGaps.Count);
        int weeksBeforeContinental = freeWeekCount - freeWeeksInGaps;
        int restWeekCount = freeWeeksInGaps - windowCount - cupRounds;

        HashSet<int> cupGaps = DomesticCupGaps(cupRounds);
        HashSet<int> restGaps = RestGaps(cupGaps, restWeekCount);

        (List<int> windowWeeks, List<int> domesticCupWeeks, List<int> continentalWeeks) = WalkGaps(
            weeksBeforeContinental + FirstGameWeek,
            cupGaps,
            restGaps);

        List<int> leagueWeeks = LeagueWeeksOf(gameWeekCount, windowWeeks, leagueRounds);

        int playOffIndex = CalendarTemplate.LeaguePhaseDateCount;
        int roundOf16Index = playOffIndex + LegsPerTie;
        int quarterFinalIndex = roundOf16Index + LegsPerTie;
        int semiFinalIndex = quarterFinalIndex + LegsPerTie;
        int finalIndex = semiFinalIndex + LegsPerTie;

        return new SeasonLayout(
            leagueWeeks,
            windowWeeks,
            domesticCupWeeks,
            continentalWeeks.Take(CalendarTemplate.LeaguePhaseDateCount).ToList(),
            TieWeeksOf(continentalWeeks, playOffIndex),
            TieWeeksOf(continentalWeeks, roundOf16Index),
            TieWeeksOf(continentalWeeks, quarterFinalIndex),
            TieWeeksOf(continentalWeeks, semiFinalIndex),
            continentalWeeks[finalIndex]);
    }

    /// <summary>
    /// Counts the rounds of the domestic cup: the smallest whole number <c>k</c> for which 2 to the power <c>k</c> is
    /// at least the number of clubs, so that <c>k</c> knockout rounds end with one winner. It uses whole numbers only,
    /// so no floating-point rounding can change the count.
    /// </summary>
    /// <param name="clubCount">The number of clubs in the league.</param>
    /// <returns>The number of domestic cup rounds, the last of which is the final.</returns>
    private static int DomesticCupRoundCount(int clubCount)
    {
        return BitOperations.Log2(BitOperations.RoundUpToPowerOf2((uint)clubCount));
    }

    /// <summary>
    /// Spreads the domestic cup rounds, from the first round to the final, over the open gaps the cup may use. Round
    /// <c>i</c>, counted from 0, takes the gap at the whole position nearest to <c>i * (g - 1) / (rounds - 1)</c>,
    /// where <c>g</c> is the number of those gaps; an exact half goes to the even position.
    /// </summary>
    /// <param name="roundCount">
    /// The number of domestic cup rounds; it must be at least 2 and no more than the number of gaps the cup may use,
    /// which the template's limit on clubs guarantees.
    /// </param>
    /// <returns>The gaps that hold a domestic cup round.</returns>
    private static HashSet<int> DomesticCupGaps(int roundCount)
    {
        // Gap 0 is left out so that the domestic cup does not start between the first two continental dates.
        List<int> usableGaps = CalendarTemplate.OpenGaps.Where(gap => gap != FirstGap).ToList();

        HashSet<int> cupGaps = new();

        for (int round = 0; round < roundCount; round++)
        {
            // The default of Math.Round sends an exact half to the even whole number, and the positions rely on that.
            double position = Math.Round((double)(round * (usableGaps.Count - 1)) / (roundCount - 1));

            cupGaps.Add(usableGaps[(int)position]);
        }

        return cupGaps;
    }

    /// <summary>
    /// Chooses the open gaps that hold a rest week: the last ones, in gap order, that the domestic cup does not use.
    /// The other open gaps stay empty.
    /// </summary>
    /// <param name="cupGaps">The gaps that hold a domestic cup round.</param>
    /// <param name="restWeekCount">The number of rest weeks; it is at most the number of open gaps left over.</param>
    /// <returns>The gaps that hold a rest week.</returns>
    private static HashSet<int> RestGaps(HashSet<int> cupGaps, int restWeekCount)
    {
        List<int> unusedGaps = CalendarTemplate.OpenGaps.Where(gap => !cupGaps.Contains(gap)).ToList();

        return unusedGaps.Skip(unusedGaps.Count - restWeekCount).ToHashSet();
    }

    /// <summary>
    /// Walks the gaps from the first to the last and puts every continental date and the free week of every gap into
    /// a game week. A continental date takes the next game week, and the free week of the gap after it, if the gap
    /// holds one, takes the week after that. The final follows the last gap.
    /// </summary>
    /// <param name="firstContinentalWeek">The game week of the first continental date.</param>
    /// <param name="cupGaps">The gaps that hold a domestic cup round.</param>
    /// <param name="restGaps">The gaps that hold a rest week.</param>
    /// <returns>
    /// The game weeks of the international windows, of the domestic cup rounds and of the continental dates, each
    /// earliest first.
    /// </returns>
    private static (List<int> WindowWeeks, List<int> DomesticCupWeeks, List<int> ContinentalWeeks) WalkGaps(
        int firstContinentalWeek,
        HashSet<int> cupGaps,
        HashSet<int> restGaps)
    {
        List<int> windowWeeks = new();
        List<int> domesticCupWeeks = new();
        List<int> continentalWeeks = new();

        int week = firstContinentalWeek;
        int gapCount = CalendarTemplate.ContinentalDateCount - 1;

        for (int gap = 0; gap < gapCount; gap++)
        {
            continentalWeeks.Add(week);
            week++;

            if (CalendarTemplate.WindowGaps.Contains(gap))
            {
                windowWeeks.Add(week);
                week++;
            }
            else if (cupGaps.Contains(gap))
            {
                domesticCupWeeks.Add(week);
                week++;
            }
            else if (restGaps.Contains(gap))
            {
                week++;
            }
        }

        continentalWeeks.Add(week);

        return (windowWeeks, domesticCupWeeks, continentalWeeks);
    }

    /// <summary>
    /// Gives the game weeks that have a league round: every club week except the ones the league skips. The skipped
    /// weeks are spread evenly over the skippable weeks.
    /// </summary>
    /// <param name="gameWeekCount">The number of game weeks in the season.</param>
    /// <param name="windowWeeks">The game weeks of the international windows.</param>
    /// <param name="leagueRounds">The number of league rounds.</param>
    /// <returns>The game weeks of the league rounds, earliest first.</returns>
    private static List<int> LeagueWeeksOf(int gameWeekCount, IReadOnlyList<int> windowWeeks, int leagueRounds)
    {
        List<int> clubWeeks = Enumerable.Range(FirstGameWeek, gameWeekCount)
            .Where(week => !windowWeeks.Contains(week))
            .ToList();

        int skippedCount = clubWeeks.Count - leagueRounds;
        List<int> skippable = SkippableWeeks(clubWeeks, windowWeeks, gameWeekCount);

        // Each skipped week is the middle of its own equal share of the skippable weeks, which spreads them evenly.
        // The validator's limits leave at least as many skippable weeks as skipped weeks, so no two shares pick the
        // same week.
        HashSet<int> skipped = Enumerable.Range(0, skippedCount)
            .Select(index => skippable[(2 * index + 1) * skippable.Count / (2 * skippedCount)])
            .ToHashSet();

        return clubWeeks.Where(week => !skipped.Contains(week)).ToList();
    }

    /// <summary>
    /// Lists the club weeks the league may skip, earliest first. A club week is not skippable when it is the first or
    /// the last game week, when it is next to a window, or when it comes directly after a skippable week.
    /// </summary>
    /// <param name="clubWeeks">The game weeks that are not international windows, earliest first.</param>
    /// <param name="windowWeeks">The game weeks of the international windows.</param>
    /// <param name="gameWeekCount">The number of game weeks in the season.</param>
    /// <returns>The skippable game weeks, earliest first.</returns>
    private static List<int> SkippableWeeks(
        IReadOnlyList<int> clubWeeks,
        IReadOnlyList<int> windowWeeks,
        int gameWeekCount)
    {
        List<int> skippable = new();

        foreach (int week in clubWeeks)
        {
            bool isFirstOrLast = week == FirstGameWeek || week == gameWeekCount;
            bool isNextToWindow = windowWeeks.Contains(week - 1) || windowWeeks.Contains(week + 1);
            bool followsSkippable = skippable.Count > 0 && skippable[^1] == week - 1;

            // A week with no league round next to a window, or next to another such week, would make a club that
            // plays only the league wait 3 game weeks.
            if (!isFirstOrLast && !isNextToWindow && !followsSkippable)
            {
                skippable.Add(week);
            }
        }

        return skippable;
    }

    /// <summary>
    /// Gives the game weeks of the two legs of a tie from the game weeks of the continental dates.
    /// </summary>
    /// <param name="continentalWeeks">The game weeks of the continental dates, earliest first.</param>
    /// <param name="firstLegIndex">The position of the tie's first leg among the continental dates.</param>
    /// <returns>The game weeks of the first leg and of the second leg.</returns>
    private static (int FirstLeg, int SecondLeg) TieWeeksOf(IReadOnlyList<int> continentalWeeks, int firstLegIndex)
    {
        return (continentalWeeks[firstLegIndex], continentalWeeks[firstLegIndex + 1]);
    }
}
