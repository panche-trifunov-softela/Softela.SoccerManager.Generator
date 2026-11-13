namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// The knockout rounds of a continental cup, named as <see cref="Calendar.ContinentalCupDates"/> names their dates.
/// The backend stores them as match stages 5 (play-off) to 9 (final).
/// </summary>
public enum ContinentalRound
{
    /// <summary>
    /// The play-off, which only a 32-club cup has.
    /// </summary>
    PlayOff,

    /// <summary>
    /// The round of 16, which only a 32-club cup has.
    /// </summary>
    RoundOf16,

    /// <summary>
    /// The quarter-final, the first round of a 16-club cup.
    /// </summary>
    QuarterFinal,

    /// <summary>
    /// The semi-final.
    /// </summary>
    SemiFinal,

    /// <summary>
    /// The final, played as one match.
    /// </summary>
    Final,
}
