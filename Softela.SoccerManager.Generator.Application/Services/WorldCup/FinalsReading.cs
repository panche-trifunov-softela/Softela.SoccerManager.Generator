using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// What the written matches of the World Cup finals say about them. The status decides which members are set:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="FinalsReadingStatus.WaitingForGroups"/>: the count of finished group matches only; no group tables, no
/// bracket, no written ties, no winners, no new ties, no third place and no last group kickoff.
/// </description>
/// </item>
/// <item>
/// <description><see cref="FinalsReadingStatus.Ready"/>: every member is set.</description>
/// </item>
/// </list>
/// </summary>
/// <param name="Status">How far the finals have got.</param>
/// <param name="FinishedGroupMatches">The number of group matches that are finished.</param>
/// <param name="GroupTables">
/// The final table of each of the 8 groups, in group order, each with its 4 league team ids in place order.
/// </param>
/// <param name="Bracket">The knockout bracket built from the group tables.</param>
/// <param name="WrittenTies">The ties of the bracket that have a match, in round order and then tie order.</param>
/// <param name="Winners">The decided ties with their winners, in round order and then tie order.</param>
/// <param name="NewTies">
/// The ties whose two nations are known and that have no match yet, in round order and then tie order. The first nation
/// of a tie is the home nation.
/// </param>
/// <param name="ThirdPlace">
/// The third-place match: set once both semi-finals are decided, with its kickoff once it is written.
/// </param>
/// <param name="LastGroupKickoff">The latest kickoff of the group matches, in UTC.</param>
internal sealed record FinalsReading(
    FinalsReadingStatus Status,
    int FinishedGroupMatches,
    IReadOnlyList<IReadOnlyList<int>> GroupTables,
    KnockoutBracket? Bracket,
    IReadOnlyList<FinalsWrittenTie> WrittenTies,
    IReadOnlyList<KnockoutWinner> Winners,
    IReadOnlyList<KnockoutKnownTie> NewTies,
    FinalsThirdPlace? ThirdPlace,
    DateTime? LastGroupKickoff);
