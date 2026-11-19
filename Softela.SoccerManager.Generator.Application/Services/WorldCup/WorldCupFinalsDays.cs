using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The days the World Cup finals are played on, or the continental cup the finals still wait for.
/// </summary>
/// <param name="Days">
/// The kickoffs of the 8 finals days, earliest first; empty when <paramref name="WaitingForCup"/> is set.
/// </param>
/// <param name="WaitingForCup">
/// The first continental cup that has matches this season but no final yet, or null when none is waiting.
/// </param>
internal sealed record WorldCupFinalsDays(
    IReadOnlyList<DateTime> Days,
    GetCompetitionsResult? WaitingForCup);
