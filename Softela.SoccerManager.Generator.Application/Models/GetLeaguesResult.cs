namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetLeagues</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others.
/// </summary>
public sealed class GetLeaguesResult
{
    /// <summary>
    /// The identifier of the league.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The name of the league.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
