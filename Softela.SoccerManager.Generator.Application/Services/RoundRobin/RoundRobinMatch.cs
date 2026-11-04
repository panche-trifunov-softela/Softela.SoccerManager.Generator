namespace Softela.SoccerManager.Generator.Application.Services.RoundRobin;

/// <summary>
/// One generated match of a round robin.
/// </summary>
/// <param name="Round">The 1-based round the match is played in.</param>
/// <param name="HomeClubId">The id of the club at home.</param>
/// <param name="AwayClubId">The id of the club away.</param>
public sealed record RoundRobinMatch(int Round, int HomeClubId, int AwayClubId);
