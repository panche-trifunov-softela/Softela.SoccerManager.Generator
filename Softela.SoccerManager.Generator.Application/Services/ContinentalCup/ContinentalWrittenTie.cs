namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// A tie of the continental bracket that has written matches.
/// </summary>
/// <param name="Round">The round of the tie.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="LastKickoff">
/// The kickoff of the tie's later leg, in UTC; for the final, its only kickoff. The ties that follow are dated after
/// it.
/// </param>
public sealed record ContinentalWrittenTie(ContinentalRound Round, int TieNumber, DateTime LastKickoff);
