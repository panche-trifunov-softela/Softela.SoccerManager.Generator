using Softela.SoccerManager.Generator.Application.Services.Calendar;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// What the season's league matches say about its calendar.
/// </summary>
/// <param name="Calendar">
/// The calendar the league was written on, or null while the season has no league match.
/// </param>
/// <param name="ClubCounts">
/// The number of clubs in each division, in division order; empty while <paramref name="Calendar"/> is null.
/// </param>
internal sealed record WorldCupCalendarReading(
    SeasonCalendar? Calendar,
    IReadOnlyList<int> ClubCounts);
