using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// What a season's domestic cup says about whether the season may end: the cup's final, or nothing when the season is
/// played without a cup.
/// </summary>
/// <param name="WaitingFor">
/// What the season waits for, as a phrase that completes "it waits for ..."; null when the cup is decided or the season
/// is played without a cup.
/// </param>
/// <param name="DecidingMatches">
/// The cup's matches when it is decided; empty while the season waits, and when the season is played without a cup.
/// </param>
internal sealed record DomesticCupEnd(string? WaitingFor, IReadOnlyList<SeasonMatch> DecidingMatches);
