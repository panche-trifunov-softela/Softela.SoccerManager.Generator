using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// What a league's previous season gives the continental cups of the season after it. The status decides which members
/// are set:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="PreviousSeasonStatus.None"/>: no season, no table order, no cup winner and no reason.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PreviousSeasonStatus.Usable"/>: the season, the table order and, when the domestic cup was played to its
/// final, its winner; when the cup cannot be read, the reason in place of the winner.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PreviousSeasonStatus.NotUsable"/>: the season and the reason, and nothing else.
/// </description>
/// </item>
/// </list>
/// </summary>
/// <param name="Status">Whether the previous season can be used.</param>
/// <param name="Season">The previous season, the one with the highest number below the season handled; or null.</param>
/// <param name="TableOrder">
/// The previous season's division tables joined in division order, place 1 first; empty unless usable.
/// </param>
/// <param name="CupWinnerLeagueTeamId">
/// The league team id of the previous season's domestic cup winner, or null when it had no final, no cup, or a cup
/// that cannot be read (the reason then says why).
/// </param>
/// <param name="Reason">
/// Why the season cannot be used when it is not usable; when it is usable, why its domestic cup cannot be read;
/// otherwise null.
/// </param>
internal sealed record PreviousSeasonReading(
    PreviousSeasonStatus Status,
    GetSeasonsResult? Season,
    IReadOnlyList<int> TableOrder,
    int? CupWinnerLeagueTeamId,
    string? Reason);
