namespace Softela.SoccerManager.Generator.Application.Services.Knockout;

/// <summary>
/// The shape of a knockout competition: every round and every tie, with each side either a known club or the winner
/// of an earlier tie. The order of the two sides in a tie carries no meaning: whoever writes the tie decides who
/// hosts.
/// </summary>
/// <param name="Rounds">
/// The ties of each round; the first item is round 1 and the last item is the final. Each round's ties are in
/// tie-number order, numbered from 1.
/// </param>
public sealed record KnockoutBracket(IReadOnlyList<IReadOnlyList<KnockoutTie>> Rounds);
