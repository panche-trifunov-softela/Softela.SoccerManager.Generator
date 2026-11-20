using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// What one division gave the reading: its table and playoff winner, or what the season waits for.
/// </summary>
/// <param name="WaitingFor">What the season waits for; null when the division is finished.</param>
/// <param name="Table">The league team ids in place order, place 1 first; empty while waiting.</param>
/// <param name="PlayoffWinner">The division's playoff winner; null without a playoff, and while waiting.</param>
/// <param name="PlayoffMatches">
/// The playoff matches that decided; empty without a playoff, and while waiting.
/// </param>
internal sealed record DivisionEnd(
    string? WaitingFor,
    IReadOnlyList<int> Table,
    int? PlayoffWinner,
    IReadOnlyList<SeasonMatch> PlayoffMatches);
