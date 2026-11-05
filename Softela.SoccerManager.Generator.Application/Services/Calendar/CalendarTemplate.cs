namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The rules a season's layout is computed from. A week runs from Thursday to Wednesday, and weeks are numbered from 1.
/// The league plays on Thursday and Sunday, the World Cup qualifiers on Friday, the continental cups on Monday and the
/// domestic cup on Tuesday. In the club season, Wednesday and Saturday stay free; the World Cup finals use every day.
/// </summary>
internal static class CalendarTemplate
{
    public const int FirstWeek = 1;
    public const int DaysPerWeek = 7;

    public const int ThursdayOffset = 0;
    public const int FridayOffset = 1;
    public const int SundayOffset = 3;
    public const int MondayOffset = 4;
    public const int TuesdayOffset = 5;

    public const int MinHoursBetweenKickoffs = 24;

    // Two match days in a row are 24 hours apart only because every match kicks off at the same time.
    public static readonly TimeOnly KickoffTime = new(20, 0);

    public const int MinClubsPerDivision = 10;
    public const int MaxClubsPerDivision = 20;
    public const int MaxDivisionsPerLeague = 5;

    public const int MinClubsPerDivisionForLargeContinentalFormat = 18;

    public const int SmallFormatLeaguePhaseDateCount = 6;
    public const int SmallFormatContinentalDateCount = 11;

    public const int LargeFormatLeaguePhaseDateCount = 8;
    public const int LargeFormatContinentalDateCount = 17;

    public const int FirstDomesticCupWeek = 2;
    public const int WeeksFromDomesticCupFinalToLastWeek = 1;

    public const int QualifierMatchdayCount = 6;
    public const int FirstQualifierWeek = 3;
    public const int WeeksFromLastQualifierToLastWeek = 2;

    public const int DaysFromLastClubDateToWorldCupFinals = 7;
    public const int WorldCupFinalsMatchdayCount = 8;
    public const int DaysBetweenWorldCupFinalsMatchdays = 1;

    /// <summary>
    /// Tells which continental format a league plays. A division of at least
    /// <see cref="MinClubsPerDivisionForLargeContinentalFormat"/> clubs plays the large format, with 32 clubs per cup,
    /// a play-off and a round of 16. A smaller division plays the small format, with 16 clubs per cup and neither.
    /// </summary>
    /// <param name="clubsPerDivision">The number of clubs in each division.</param>
    /// <returns><c>true</c> for the large format, <c>false</c> for the small format.</returns>
    public static bool IsLargeContinentalFormat(int clubsPerDivision)
    {
        return clubsPerDivision >= MinClubsPerDivisionForLargeContinentalFormat;
    }
}
