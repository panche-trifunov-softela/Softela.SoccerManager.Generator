namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// What a cup plan asks the step to do.
/// </summary>
public enum DomesticCupPlanStatus
{
    /// <summary>
    /// Insert the plan's ties.
    /// </summary>
    Write,

    /// <summary>
    /// Insert nothing: every tie that can be written already is.
    /// </summary>
    NothingNew,

    /// <summary>
    /// Insert nothing: round 1 cannot be played before round 2, so the season plays without the cup.
    /// </summary>
    PlaysWithoutCup,
}
