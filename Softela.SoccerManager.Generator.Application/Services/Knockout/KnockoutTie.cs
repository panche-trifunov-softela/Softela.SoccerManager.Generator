namespace Softela.SoccerManager.Generator.Application.Services.Knockout;

/// <summary>
/// One tie of a knockout bracket. The order of its two sides carries no meaning: whoever writes the tie decides who
/// hosts.
/// </summary>
/// <param name="RoundNumber">The 1-based round the tie belongs to; the last round is the final.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="First">One side of the tie.</param>
/// <param name="Second">The other side of the tie.</param>
public sealed record KnockoutTie(int RoundNumber, int TieNumber, KnockoutSide First, KnockoutSide Second);
