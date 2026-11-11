namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// One tie of a domestic cup bracket. The order of its two sides carries no meaning, because the home side is chosen
/// when the tie is written.
/// </summary>
/// <param name="RoundNumber">The 1-based round the tie belongs to; the last round is the final.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="First">One side of the tie.</param>
/// <param name="Second">The other side of the tie.</param>
public sealed record DomesticCupTie(int RoundNumber, int TieNumber, DomesticCupSide First, DomesticCupSide Second);
