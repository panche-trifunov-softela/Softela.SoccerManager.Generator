namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// One entry of a round robin table, written in positions rather than clubs.
/// </summary>
/// <param name="Home">The 0-based position that plays at home.</param>
/// <param name="Away">The 0-based position that plays away.</param>
public readonly record struct PositionMatch(int Home, int Away);
