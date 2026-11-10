namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetMatches</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others, among them the commentary, which is a native JSON column.
/// </summary>
public sealed class GetMatchesResult
{
    /// <summary>
    /// The identifier of the match.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The identifier of the season the match belongs to.
    /// </summary>
    public int SeasonId { get; set; }

    /// <summary>
    /// The identifier of the competition the match is played in, or <see langword="null"/> for a friendly.
    /// </summary>
    public int? CompetitionId { get; set; }

    /// <summary>
    /// The UTC kickoff of the match.
    /// </summary>
    public DateTime StartDateTime { get; set; }
}
