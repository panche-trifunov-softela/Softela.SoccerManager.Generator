namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// What the written knockout matches say about the bracket: which ties are decided and which have matches.
/// </summary>
/// <param name="Winners">The decided ties with their winners, in round order and then tie order.</param>
/// <param name="WrittenTies">The ties that have matches, in round order and then tie order.</param>
public sealed record ContinentalKnockoutProgress(
    IReadOnlyList<ContinentalWinner> Winners,
    IReadOnlyList<ContinentalWrittenTie> WrittenTies);
