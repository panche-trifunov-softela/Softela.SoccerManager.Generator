namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// How far a division's promotion playoff has got, as the division's matches and Standings show it.
/// </summary>
public enum PlayoffReadingStatus
{
    /// <summary>
    /// Some league match of the division is not finished, so the table is not final.
    /// </summary>
    WaitingForLeague,

    /// <summary>
    /// The league is finished, but the division's Standings changed less than an hour ago, so a person may still be
    /// entering them.
    /// </summary>
    WaitingForStandings,

    /// <summary>
    /// The league is finished and its Standings are settled and agree with the matches; the table is ranked and the
    /// playoff's progress is read.
    /// </summary>
    Ready,
}
