namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// A tie of the draw and the written match that plays it.
/// </summary>
/// <param name="RoundNumber">The 1-based round of the tie.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="MatchId">The id of the match that plays the tie.</param>
/// <param name="Kickoff">The kickoff of that match, in UTC.</param>
public sealed record DomesticCupWrittenTie(int RoundNumber, int TieNumber, int MatchId, DateTime Kickoff);
