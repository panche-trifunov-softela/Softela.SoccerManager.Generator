using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Finds the domestic cup among a league's competitions. The domestic cup step and the season rollover both need the
/// same competition, so the shape that identifies it lives here once.
/// </summary>
internal static class DomesticCupFinder
{
    /// <summary>
    /// The competition format of a domestic cup: a knockout only, as the backend's
    /// <c>CompetitionFormat.KnockoutOnly</c>.
    /// </summary>
    internal const byte KnockoutOnlyFormat = 1;

    /// <summary>
    /// Picks the competitions with the domestic cup's shape out of a league's competitions.
    /// </summary>
    /// <param name="competitions">The league's competitions.</param>
    /// <returns>The competitions shaped like a domestic cup, ordered by competition id.</returns>
    internal static IReadOnlyList<GetCompetitionsResult> Find(IReadOnlyList<GetCompetitionsResult> competitions)
    {
        return competitions
            .Where(IsDomesticCup)
            .OrderBy(competition => competition.Id)
            .ToList();
    }

    /// <summary>
    /// Tells whether a competition has the shape of a domestic cup: it is not a division, it is domestic, it is a
    /// knockout, it is not national and it has no age limit, which sets it apart from a youth cup.
    /// </summary>
    /// <param name="competition">The competition.</param>
    /// <returns>True when the competition is shaped like a domestic cup.</returns>
    private static bool IsDomesticCup(GetCompetitionsResult competition)
    {
        return competition.Order == 0
            && competition.IsDomestic
            && !competition.IsNational
            && competition.Format == KnockoutOnlyFormat
            && competition.MaxAgeAllowed is null;
    }
}
