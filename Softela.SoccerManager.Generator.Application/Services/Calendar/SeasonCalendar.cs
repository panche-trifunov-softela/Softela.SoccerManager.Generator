namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The dates of one season: the league, the domestic cup, the continental cups and the World Cup.
/// </summary>
/// <param name="StartDate">
/// The date the calendar was built from, as given. It is not a kickoff: the first league matchday is after it.
/// </param>
/// <param name="LeagueMatchdays">
/// The kickoff of each league round, in UTC, earliest first, so the first is round 1.
/// </param>
/// <param name="DomesticCupRounds">
/// The kickoff of each domestic cup round, in UTC, earliest first; the last is the final.
/// </param>
/// <param name="ContinentalCups">
/// The dates of the continental cups, in UTC. Both continental cups are played on these same dates.
/// </param>
/// <param name="WorldCupQualifierMatchdays">
/// The kickoffs of the World Cup qualifier matchdays, in UTC, earliest first; each international window has two.
/// </param>
/// <param name="WorldCupFinalsMatchdays">
/// The kickoffs of the World Cup finals matchdays, in UTC, earliest first; all of them are after the last club date.
/// </param>
public sealed record SeasonCalendar(
    DateOnly StartDate,
    IReadOnlyList<DateTime> LeagueMatchdays,
    IReadOnlyList<DateTime> DomesticCupRounds,
    ContinentalCupDates ContinentalCups,
    IReadOnlyList<DateTime> WorldCupQualifierMatchdays,
    IReadOnlyList<DateTime> WorldCupFinalsMatchdays);
