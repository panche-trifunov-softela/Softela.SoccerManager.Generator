namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The rules a season's layout is computed from. A game week is 4 days: day 0 is the league matchday and day 2 is the
/// midweek slot. Game weeks are numbered from 1.
/// </summary>
internal static class CalendarTemplate
{
    public const int DaysPerGameWeek = 4;
    public const int MidweekDayOffset = 2;

    public const int MinHoursBetweenKickoffs = 48;

    // Day 0 and day 2 of a game week are 48 hours apart only because every match kicks off at the same time.
    public static readonly TimeOnly KickoffTime = new(20, 0);

    public const int MinClubsPerDivision = 10;

    // The domestic cup can use every open gap but the first, which makes room for 10 rounds, enough for 1024 clubs.
    public const int MaxClubsInLeague = 1024;

    public const int LeagueLegs = 2;

    public const int LeaguePhaseDateCount = 8;

    // A gap lies between two consecutive continental dates, so gap 0 is between the first and the second. A gap holds
    // at most one week with no continental date, so apart from the top-8 wait below, a club that plays only
    // continental matches never waits more than 2 game weeks (8 days).
    public const int ContinentalDateCount = 17;

    // A top-8 club skips the play-off, so it waits 16 days between the last league-phase matchday and the round of 16.
    // The format forces that wait. Gaps 7 and 9, either side of the play-off, stay empty to keep it at 16 days, and the
    // second window sits between the play-off legs so that the wait crosses a window.
    public static readonly IReadOnlyList<int> WindowGaps = [3, 8, 12];

    // The gaps that may hold a domestic cup round or a rest week (a league round and no midweek match).
    public static readonly IReadOnlyList<int> OpenGaps = [0, 1, 2, 4, 5, 6, 10, 11, 13, 14, 15];

    public const int DaysFromLastClubDateToWorldCupFinals = 4;
    public const int WorldCupFinalsMatchdayCount = 8;
    public const int DaysBetweenWorldCupFinalsMatchdays = 2;
}
