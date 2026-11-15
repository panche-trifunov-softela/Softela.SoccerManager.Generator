namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// How much of a season's two continental cups is written.
/// </summary>
internal enum ContinentalCupStateStatus
{
    /// <summary>
    /// Neither cup has a match or a standings row, so the league phase can be written.
    /// </summary>
    NotWritten,

    /// <summary>
    /// Both cups have matches and standings rows, and no club is in both cups.
    /// </summary>
    Written,

    /// <summary>
    /// Anything else: a cup lacks its matches or its standings, only one cup is written, or a club is in both cups.
    /// </summary>
    Partial,
}
