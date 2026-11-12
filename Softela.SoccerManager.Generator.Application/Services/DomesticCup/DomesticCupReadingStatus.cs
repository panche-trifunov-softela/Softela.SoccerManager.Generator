namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// How far a season's domestic cup has got, as its written matches show it.
/// </summary>
public enum DomesticCupReadingStatus
{
    /// <summary>
    /// The season has no league matches yet, so the cup does not know its clubs.
    /// </summary>
    WaitingForLeague,

    /// <summary>
    /// The league is written and the cup has no match yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The cup has at least one match.
    /// </summary>
    Started,
}
