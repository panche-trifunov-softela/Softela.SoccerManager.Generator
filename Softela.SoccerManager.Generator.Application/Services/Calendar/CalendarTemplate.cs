namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The rules a season's layout is computed from. A week runs from Thursday to Wednesday, and weeks are numbered from 1.
/// The league plays on Thursday and Sunday and the domestic cup on Tuesday. A league whose divisions have 12 clubs or
/// more also plays the World Cup qualifiers on Friday and the continental cups on Monday, and then the World Cup
/// finals. In the club season, Wednesday and Saturday stay free; the World Cup finals use every day.
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

    /// <summary>
    /// The smallest division size whose league plays international competitions: the continental cups, the World Cup
    /// qualifiers and the World Cup finals. It is the smallest even size above <see cref="MinClubsPerDivision"/>, so a
    /// league of 10-club divisions plays only its league and its domestic cup.
    /// </summary>
    public const int MinClubsPerDivisionForInternationalCompetitions = 12;

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
    /// Tells whether a league plays international competitions: the continental cups, the World Cup qualifiers and the
    /// World Cup finals. A division of at least <see cref="MinClubsPerDivisionForInternationalCompetitions"/> clubs
    /// plays them; a smaller division plays only its league and its domestic cup.
    /// </summary>
    /// <param name="clubsPerDivision">The number of clubs in each division.</param>
    /// <returns><c>true</c> when the league plays international competitions, <c>false</c> when it does not.</returns>
    public static bool HasInternationalCompetitions(int clubsPerDivision)
    {
        return clubsPerDivision >= MinClubsPerDivisionForInternationalCompetitions;
    }

    /// <summary>
    /// Tells which continental format a league plays. It only means something for divisions that have international
    /// competitions, which is to say at least <see cref="MinClubsPerDivisionForInternationalCompetitions"/> clubs. A
    /// division of at least <see cref="MinClubsPerDivisionForLargeContinentalFormat"/> clubs plays the large format,
    /// with 32 clubs per cup, a play-off and a round of 16. A division of 12 to 16 clubs plays the small format,
    /// with 16 clubs per cup and neither.
    /// </summary>
    /// <param name="clubsPerDivision">The number of clubs in each division.</param>
    /// <returns><c>true</c> for the large format, <c>false</c> for the small format.</returns>
    public static bool IsLargeContinentalFormat(int clubsPerDivision)
    {
        return clubsPerDivision >= MinClubsPerDivisionForLargeContinentalFormat;
    }
}
