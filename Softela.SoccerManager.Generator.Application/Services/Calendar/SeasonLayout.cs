namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The game weeks of one season: the league, the domestic cup, the international windows and the continental cups.
/// Every week is a 1-based game week, and every list is in ascending order.
/// </summary>
/// <param name="LeagueWeeks">
/// The 1-based game weeks that have a league round, earliest first, so the first is round 1.
/// </param>
/// <param name="WindowWeeks">
/// The 1-based game weeks of the international windows, earliest first; no club plays in them.
/// </param>
/// <param name="DomesticCupWeeks">
/// The 1-based game weeks of the domestic cup rounds, earliest first; the last is the final.
/// </param>
/// <param name="LeaguePhaseWeeks">
/// The 1-based game weeks of the continental league phase matchdays, earliest first.
/// </param>
/// <param name="PlayOffWeeks">
/// The 1-based game weeks of the two play-off legs; both are after the league phase.
/// </param>
/// <param name="RoundOf16Weeks">
/// The 1-based game weeks of the two round of 16 legs; both are after the play-off.
/// </param>
/// <param name="QuarterFinalWeeks">
/// The 1-based game weeks of the two quarter-final legs; both are after the round of 16.
/// </param>
/// <param name="SemiFinalWeeks">
/// The 1-based game weeks of the two semi-final legs; both are after the quarter-final.
/// </param>
/// <param name="ContinentalFinalWeek">
/// The 1-based game week of the continental final; it is after the semi-final and is the last game week of the season.
/// </param>
internal sealed record SeasonLayout(
    IReadOnlyList<int> LeagueWeeks,
    IReadOnlyList<int> WindowWeeks,
    IReadOnlyList<int> DomesticCupWeeks,
    IReadOnlyList<int> LeaguePhaseWeeks,
    (int FirstLeg, int SecondLeg) PlayOffWeeks,
    (int FirstLeg, int SecondLeg) RoundOf16Weeks,
    (int FirstLeg, int SecondLeg) QuarterFinalWeeks,
    (int FirstLeg, int SecondLeg) SemiFinalWeeks,
    int ContinentalFinalWeek);
