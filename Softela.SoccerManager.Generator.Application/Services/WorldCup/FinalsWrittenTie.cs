namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// A tie of the finals' bracket that has a written match. A knockout tie is one single match.
/// </summary>
/// <param name="RoundNumber">The 1-based round of the tie in the bracket; 1 is the round of 16 and 4 the final.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="Kickoff">The kickoff of the tie's match, in UTC.</param>
internal sealed record FinalsWrittenTie(int RoundNumber, int TieNumber, DateTime Kickoff);
