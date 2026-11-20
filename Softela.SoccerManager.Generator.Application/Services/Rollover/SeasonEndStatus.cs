namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// Whether a league's season has ended, as its matches and Standings show it.
/// </summary>
internal enum SeasonEndStatus
{
    /// <summary>
    /// The season is still running: something it ends on is not written yet, not finished yet or not settled yet.
    /// </summary>
    Running,

    /// <summary>
    /// Everything the season ends on is finished, so the tables are final and the next season can be made from them.
    /// </summary>
    Ended,
}
