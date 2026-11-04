namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The dates of a continental cup, from its league phase to its final. Both continental cups of a season are played on
/// these same dates.
/// </summary>
/// <param name="LeaguePhase">The kickoffs of the league phase matchdays, in UTC, earliest first.</param>
/// <param name="PlayOff">The kickoffs of the two play-off legs, in UTC; both are after the league phase.</param>
/// <param name="RoundOf16">The kickoffs of the two round of 16 legs, in UTC; both are after the play-off.</param>
/// <param name="QuarterFinal">
/// The kickoffs of the two quarter-final legs, in UTC; both are after the round of 16.
/// </param>
/// <param name="SemiFinal">The kickoffs of the two semi-final legs, in UTC; both are after the quarter-final.</param>
/// <param name="Final">The kickoff of the final, in UTC; it is after the semi-final and ends the cup.</param>
public sealed record ContinentalCupDates(
    IReadOnlyList<DateTime> LeaguePhase,
    TwoLegDates PlayOff,
    TwoLegDates RoundOf16,
    TwoLegDates QuarterFinal,
    TwoLegDates SemiFinal,
    DateTime Final);
