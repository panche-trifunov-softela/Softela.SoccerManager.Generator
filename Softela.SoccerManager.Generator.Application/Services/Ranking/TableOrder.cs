namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// The order a table is ranked in. Both orders count 3 points for a win and 1 for a draw, never a shootout, and both
/// end in the same seeded shuffle for participants that nothing else separates.
/// </summary>
public enum TableOrder
{
    /// <summary>
    /// The order of a club table: points, goal difference, goals for, then head-to-head among the participants that
    /// are still level, then the seeded shuffle.
    /// </summary>
    Club = 0,

    /// <summary>
    /// The order of a World Cup group: points, then head-to-head among the participants that are level on points, then
    /// goal difference, then goals for, then the seeded shuffle.
    /// </summary>
    WorldCup = 1,
}
