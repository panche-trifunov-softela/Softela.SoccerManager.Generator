namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetTeams</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others.
/// </summary>
public sealed class GetTeamsResult
{
    /// <summary>
    /// The identifier of the team.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The name of the team.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The identifier of the team's stadium, or <see langword="null"/> when the club has no stadium.
    /// </summary>
    public int? StadiumId { get; set; }
}
