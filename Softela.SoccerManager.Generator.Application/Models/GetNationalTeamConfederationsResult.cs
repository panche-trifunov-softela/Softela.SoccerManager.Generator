namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetNationalTeamConfederations</c> stored procedure: a national team with its
/// confederation. It holds only the columns the generator reads; Dapper ignores the others.
/// </summary>
public sealed class GetNationalTeamConfederationsResult
{
    /// <summary>
    /// The identifier of the national team.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The name of the national team.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The confederation of the national team, as the backend's confederation code, or <see langword="null"/> when the
    /// national team has none.
    /// </summary>
    public byte? Confederation { get; set; }
}
