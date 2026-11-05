namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The weeks of one season: the league, the World Cup qualifiers, the domestic cup and the continental cups. Every week
/// is 1-based, and every list is in ascending order.
/// </summary>
/// <param name="LeagueWeekCount">
/// The number of weeks the league plays, starting from the first week; each of them has two league rounds.
/// </param>
/// <param name="QualifierWeeks">The weeks of the World Cup qualifier matchdays, earliest first.</param>
/// <param name="DomesticCupWeeks">The weeks of the domestic cup rounds, earliest first; the last is the final.</param>
/// <param name="LeaguePhaseWeeks">The weeks of the continental league phase matchdays, earliest first.</param>
/// <param name="PlayOffWeeks">
/// The weeks of the two play-off legs; both are after the league phase. Null in the small continental format.
/// </param>
/// <param name="RoundOf16Weeks">
/// The weeks of the two round of 16 legs; both are after the play-off. Null in the small continental format.
/// </param>
/// <param name="QuarterFinalWeeks">
/// The weeks of the two quarter-final legs; both are after the round of 16, or after the league phase in the small
/// continental format.
/// </param>
/// <param name="SemiFinalWeeks">The weeks of the two semi-final legs; both are after the quarter-final.</param>
/// <param name="ContinentalFinalWeek">
/// The week of the continental final; it is after the semi-final and is the last week of the season.
/// </param>
internal sealed record SeasonLayout(
    int LeagueWeekCount,
    IReadOnlyList<int> QualifierWeeks,
    IReadOnlyList<int> DomesticCupWeeks,
    IReadOnlyList<int> LeaguePhaseWeeks,
    (int FirstLeg, int SecondLeg)? PlayOffWeeks,
    (int FirstLeg, int SecondLeg)? RoundOf16Weeks,
    (int FirstLeg, int SecondLeg) QuarterFinalWeeks,
    (int FirstLeg, int SecondLeg) SemiFinalWeeks,
    int ContinentalFinalWeek);
