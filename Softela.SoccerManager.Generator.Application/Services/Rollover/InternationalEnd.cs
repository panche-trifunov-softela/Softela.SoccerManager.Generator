using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// What a season's international competitions say about whether the season may end: the World Cup final, or, when the
/// season plays no World Cup, the continental finals.
/// </summary>
/// <param name="WaitingFor">
/// The first final the season waits for, as a phrase that completes "it waits for ..."; null when every final that
/// holds the season is finished.
/// </param>
/// <param name="DecidingMatches">
/// The finished finals that decided the end; empty while the season waits.
/// </param>
internal sealed record InternationalEnd(string? WaitingFor, IReadOnlyList<SeasonMatch> DecidingMatches);
