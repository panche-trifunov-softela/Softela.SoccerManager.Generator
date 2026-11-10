namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetSeasons</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others.
/// </summary>
public sealed class GetSeasonsResult
{
    /// <summary>
    /// The identifier of the season.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The number of the season within its league; the highest number is the newest season.
    /// </summary>
    public int SeasonNumber { get; set; }
}
