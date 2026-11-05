using System.Numerics;

namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// Lays out the weeks of a season from the shape of its league.
/// </summary>
internal static class SeasonLayoutBuilder
{
    private const int LegsPerTie = 2;

    /// <summary>
    /// Builds the layout of a season in 1-based weeks. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// The league plays two rounds a week from the first week until it has finished its double round robin, so it plays
    /// one week fewer than a division has clubs.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The continental cups have one date a week, in consecutive weeks that end the season, so the continental final is
    /// in the last week. A season has as many weeks as the longer of the league and the continental cups.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The domestic cup rounds are spread from week 2 to the week before the last, and the World Cup qualifier
    /// matchdays from week 3 to two weeks before the last.
    /// </description>
    /// </item>
    /// <item>
    /// <description>Only the large continental format has a play-off and a round of 16.</description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="clubsPerDivision">The number of clubs in each division; it must already be validated.</param>
    /// <param name="divisionCount">The number of divisions; it must already be validated.</param>
    /// <returns>The weeks of the season's league, World Cup qualifiers, domestic cup and continental cups.</returns>
    public static SeasonLayout Build(int clubsPerDivision, int divisionCount)
    {
        bool isLargeFormat = CalendarTemplate.IsLargeContinentalFormat(clubsPerDivision);

        int leaguePhaseDateCount = isLargeFormat
            ? CalendarTemplate.LargeFormatLeaguePhaseDateCount
            : CalendarTemplate.SmallFormatLeaguePhaseDateCount;

        int continentalDateCount = isLargeFormat
            ? CalendarTemplate.LargeFormatContinentalDateCount
            : CalendarTemplate.SmallFormatContinentalDateCount;

        int leagueWeekCount = clubsPerDivision - 1;
        int lastWeek = Math.Max(leagueWeekCount, continentalDateCount);

        List<int> qualifierWeeks = SpreadWeeks(
            CalendarTemplate.FirstQualifierWeek,
            lastWeek - CalendarTemplate.WeeksFromLastQualifierToLastWeek,
            CalendarTemplate.QualifierMatchdayCount);

        List<int> domesticCupWeeks = SpreadWeeks(
            CalendarTemplate.FirstDomesticCupWeek,
            lastWeek - CalendarTemplate.WeeksFromDomesticCupFinalToLastWeek,
            DomesticCupRoundCount(clubsPerDivision * divisionCount));

        int firstContinentalWeek = lastWeek - continentalDateCount + CalendarTemplate.FirstWeek;
        List<int> continentalWeeks = Enumerable.Range(firstContinentalWeek, continentalDateCount).ToList();

        // Both formats end with the same dates, so the positions are counted back from the final.
        int finalIndex = continentalDateCount - 1;
        int semiFinalIndex = finalIndex - LegsPerTie;
        int quarterFinalIndex = semiFinalIndex - LegsPerTie;
        int roundOf16Index = quarterFinalIndex - LegsPerTie;
        int playOffIndex = roundOf16Index - LegsPerTie;

        return new SeasonLayout(
            leagueWeekCount,
            qualifierWeeks,
            domesticCupWeeks,
            continentalWeeks.Take(leaguePhaseDateCount).ToList(),
            isLargeFormat ? TieWeeksOf(continentalWeeks, playOffIndex) : null,
            isLargeFormat ? TieWeeksOf(continentalWeeks, roundOf16Index) : null,
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
    /// Spreads dates evenly over a range of weeks, from its first week to its last. Date <c>i</c>, counted from 0,
    /// takes the week at the whole position nearest to <c>i * (last - first) / (count - 1)</c> from the first week; an
    /// exact half goes to the even position.
    /// </summary>
    /// <param name="firstWeek">The week of the first date.</param>
    /// <param name="lastWeek">The week of the last date.</param>
    /// <param name="count">
    /// The number of dates; it must be at least 2 and no more than the weeks in the range, which the template's limits
    /// on clubs and divisions guarantee.
    /// </param>
    /// <returns>The week of each date, earliest first.</returns>
    private static List<int> SpreadWeeks(int firstWeek, int lastWeek, int count)
    {
        int span = lastWeek - firstWeek;

        return Enumerable.Range(0, count)
            .Select(index => firstWeek + DivideRoundingHalfToEven(index * span, count - 1))
            .ToList();
    }

    /// <summary>
    /// Divides two whole numbers and rounds to the nearest whole number, sending an exact half to the even one. It uses
    /// whole numbers only, so no floating-point rounding can change the result.
    /// </summary>
    /// <param name="dividend">The number to divide; it must not be negative.</param>
    /// <param name="divisor">The number to divide by; it must be positive.</param>
    /// <returns>The rounded quotient.</returns>
    private static int DivideRoundingHalfToEven(int dividend, int divisor)
    {
        int quotient = dividend / divisor;
        int twiceRemainder = 2 * (dividend % divisor);

        bool isPastHalf = twiceRemainder > divisor;
        bool isOddAtHalf = twiceRemainder == divisor && quotient % 2 != 0;

        return isPastHalf || isOddAtHalf ? quotient + 1 : quotient;
    }

    /// <summary>
    /// Gives the weeks of the two legs of a tie from the weeks of the continental dates.
    /// </summary>
    /// <param name="continentalWeeks">The weeks of the continental dates, earliest first.</param>
    /// <param name="firstLegIndex">The position of the tie's first leg among the continental dates.</param>
    /// <returns>The weeks of the first leg and of the second leg.</returns>
    private static (int FirstLeg, int SecondLeg) TieWeeksOf(IReadOnlyList<int> continentalWeeks, int firstLegIndex)
    {
        return (continentalWeeks[firstLegIndex], continentalWeeks[firstLegIndex + 1]);
    }
}
