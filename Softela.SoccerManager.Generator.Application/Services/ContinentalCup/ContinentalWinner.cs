namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// A continental cup tie that has been decided. The step that reads the played matches works out the winner from the
/// scores.
/// </summary>
/// <param name="Round">The round of the decided tie.</param>
/// <param name="TieNumber">The 1-based number of the decided tie within its round.</param>
/// <param name="LeagueTeamId">The league team id of the club that won the tie.</param>
public sealed record ContinentalWinner(ContinentalRound Round, int TieNumber, int LeagueTeamId);
