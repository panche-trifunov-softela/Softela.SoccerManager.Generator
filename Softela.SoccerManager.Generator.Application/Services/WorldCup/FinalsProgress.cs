using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// What the written knockout matches of the World Cup finals say about the bracket's ties.
/// </summary>
/// <param name="Winners">The decided ties with their winners, in round order and then tie order.</param>
/// <param name="WrittenTies">The ties that have a match, in round order and then tie order.</param>
/// <param name="ThirdPlace">
/// The third-place match, or null while the two semi-finals are not both decided.
/// </param>
internal sealed record FinalsProgress(
    IReadOnlyList<KnockoutWinner> Winners,
    IReadOnlyList<FinalsWrittenTie> WrittenTies,
    FinalsThirdPlace? ThirdPlace);
