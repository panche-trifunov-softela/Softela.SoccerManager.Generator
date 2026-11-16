namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// How far a continental cup's knockout has got, as the cup's written matches show it.
/// </summary>
public enum ContinentalKnockoutReadingStatus
{
    /// <summary>
    /// Some league-phase match is not finished and no knockout match is written, so the table is not final.
    /// </summary>
    WaitingForLeaguePhase,

    /// <summary>
    /// The league phase is finished; the bracket and the knockout's progress are read.
    /// </summary>
    Ready,
}
