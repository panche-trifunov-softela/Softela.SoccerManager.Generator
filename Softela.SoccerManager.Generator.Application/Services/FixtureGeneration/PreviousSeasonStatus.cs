namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Whether a league's previous season can give the continental cups their entrants.
/// </summary>
internal enum PreviousSeasonStatus
{
    /// <summary>
    /// The league has no season before the one being handled.
    /// </summary>
    None,

    /// <summary>
    /// The previous season's divisions are finished and agree with their Standings, so their tables are final.
    /// </summary>
    Usable,

    /// <summary>
    /// The previous season exists but its tables cannot be used: it is not finished, or what it holds is not as it
    /// should be. The reading says why.
    /// </summary>
    NotUsable,
}
