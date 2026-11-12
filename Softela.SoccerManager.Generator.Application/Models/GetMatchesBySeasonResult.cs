namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetMatchesBySeason</c> stored procedure. It adds to the columns of
/// <see cref="GetMatchesResult"/> the ones that tell a played match from an unplayed one and place a staged match in
/// its stage. It holds only the columns the generator reads; Dapper ignores the others.
/// </summary>
public sealed class GetMatchesBySeasonResult : GetMatchesResult
{
    /// <summary>
    /// Whether the match is finished.
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// The stage the match belongs to, as the backend's match stage code, or <see langword="null"/> for a league
    /// match or a friendly.
    /// </summary>
    public byte? Stage { get; set; }

    /// <summary>
    /// The round of the stage the match is played in, or <see langword="null"/> when the stage has no rounds.
    /// </summary>
    public byte? RoundNumber { get; set; }

    /// <summary>
    /// The leg of a tie the match is, or <see langword="null"/> for a single match.
    /// </summary>
    public byte? Leg { get; set; }
}
