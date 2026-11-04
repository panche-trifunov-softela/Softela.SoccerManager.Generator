namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// Two clubs that share a stadium, so they must never both be at home in the same round.
/// </summary>
/// <param name="FirstClubId">The id of one club of the pair.</param>
/// <param name="SecondClubId">The id of the other club of the pair.</param>
public sealed record SharedStadiumPair(int FirstClubId, int SecondClubId);
