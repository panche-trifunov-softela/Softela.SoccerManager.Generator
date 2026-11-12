namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetCompetitions</c> stored procedure. It holds only the columns the generator reads;
/// Dapper ignores the others.
/// </summary>
public sealed class GetCompetitionsResult
{
    /// <summary>
    /// The identifier of the competition.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The name of the competition.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the competition is played inside one country.
    /// </summary>
    public bool IsDomestic { get; set; }

    /// <summary>
    /// The format of the competition, as the backend's competition format code.
    /// </summary>
    public byte Format { get; set; }

    /// <summary>
    /// The position of the competition in its league; an order above 0 marks a division.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The oldest age allowed to play in the competition, or <see langword="null"/> when it has no age limit, which is
    /// how a youth cup differs from the domestic cup.
    /// </summary>
    public int? MaxAgeAllowed { get; set; }
}
