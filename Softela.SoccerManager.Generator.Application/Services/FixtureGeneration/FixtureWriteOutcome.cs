namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// What came of writing under a season's lock.
/// </summary>
internal enum FixtureWriteOutcome
{
    /// <summary>
    /// Another run held the season's lock, so nothing was read or written.
    /// </summary>
    LockHeld,

    /// <summary>
    /// The caller found nothing to write in the season's matches, so the transaction was rolled back.
    /// </summary>
    NotWritten,

    /// <summary>
    /// The caller's rows were inserted and committed.
    /// </summary>
    Written,
}
