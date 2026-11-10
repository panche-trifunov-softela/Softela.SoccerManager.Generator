namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The weeks of the continental cups, which both cups share. Only a league of 12 clubs per division or more has them.
/// Every week is 1-based and every list is in ascending order; the final is in the last week of the season.
/// </summary>
/// <param name="LeaguePhaseWeeks">The weeks of the league phase matchdays, earliest first.</param>
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
/// <param name="FinalWeek">
/// The week of the final; it is after the semi-final and is the last week of the season.
/// </param>
internal sealed record ContinentalLayout(
    IReadOnlyList<int> LeaguePhaseWeeks,
    (int FirstLeg, int SecondLeg)? PlayOffWeeks,
    (int FirstLeg, int SecondLeg)? RoundOf16Weeks,
    (int FirstLeg, int SecondLeg) QuarterFinalWeeks,
    (int FirstLeg, int SecondLeg) SemiFinalWeeks,
    int FinalWeek);
