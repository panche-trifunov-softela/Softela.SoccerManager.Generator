namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The weeks of one season: the league, the domestic cup and, for divisions of 12 clubs or more, the World Cup
/// qualifiers and the continental cups. Every week is 1-based, and every list is in ascending order.
/// </summary>
/// <param name="LeagueWeekCount">
/// The number of weeks the league plays, starting from the first week; each of them has two league rounds.
/// </param>
/// <param name="QualifierWeeks">
/// The weeks of the World Cup qualifier matchdays, earliest first. Empty when the divisions have fewer than 12 clubs.
/// </param>
/// <param name="DomesticCupWeeks">The weeks of the domestic cup rounds, earliest first; the last is the final.</param>
/// <param name="Continental">
/// The weeks of the continental cups. Null when the divisions have fewer than 12 clubs.
/// </param>
internal sealed record SeasonLayout(
    int LeagueWeekCount,
    IReadOnlyList<int> QualifierWeeks,
    IReadOnlyList<int> DomesticCupWeeks,
    ContinentalLayout? Continental);
