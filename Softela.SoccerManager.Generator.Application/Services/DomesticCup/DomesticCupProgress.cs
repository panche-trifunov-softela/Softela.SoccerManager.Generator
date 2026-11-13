using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// What the written cup matches say about the draw: which ties are decided, which have a match, and who has won the
/// cup.
/// </summary>
/// <param name="Winners">The decided ties with their winners, in round order and then tie order.</param>
/// <param name="WrittenTies">The ties that have a match, in round order and then tie order.</param>
/// <param name="ChampionLeagueTeamId">
/// The league team id of the final's winner, or null until the final is finished. The season rollover and the
/// continental cups read it.
/// </param>
public sealed record DomesticCupProgress(
    IReadOnlyList<KnockoutWinner> Winners,
    IReadOnlyList<DomesticCupWrittenTie> WrittenTies,
    int? ChampionLeagueTeamId);
