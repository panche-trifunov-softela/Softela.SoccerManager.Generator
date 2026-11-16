using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Finds the continental cups among a league's competitions. The league phase step and the knockout step both need the
/// same two cups, so the shape that identifies them lives here once. It also tells whether a cup has matches in a
/// season.
/// </summary>
internal static class ContinentalCupFinder
{
    /// <summary>
    /// The competition format of a continental cup: a league phase followed by a knockout, as the backend's
    /// <c>CompetitionFormat.GroupAndKnockout</c>.
    /// </summary>
    private const byte GroupAndKnockoutFormat = 3;

    /// <summary>
    /// Picks the continental cups out of a league's competitions.
    /// </summary>
    /// <param name="competitions">The league's competitions.</param>
    /// <returns>The continental cups, ordered by competition id.</returns>
    internal static List<GetCompetitionsResult> Find(IEnumerable<GetCompetitionsResult> competitions)
    {
        return competitions
            .Where(IsContinentalCup)
            .OrderBy(competition => competition.Id)
            .ToList();
    }

    /// <summary>
    /// Tells whether a read of the league's matches has a match of a season in a cup.
    /// </summary>
    /// <param name="matches">The league's matches.</param>
    /// <param name="seasonId">The season's id.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <returns>True when the cup has a match in the season.</returns>
    internal static bool HasMatchInSeason(
        IEnumerable<GetMatchesResult> matches,
        int seasonId,
        GetCompetitionsResult cup)
    {
        return matches.Any(match => match.SeasonId == seasonId && match.CompetitionId == cup.Id);
    }

    /// <summary>
    /// Tells whether a competition has the shape of a continental cup: it is not a division, it is not domestic, it is
    /// a league phase with a knockout and it has no age limit, which sets it apart from a youth cup.
    /// </summary>
    /// <param name="competition">The competition.</param>
    /// <returns>True when the competition is shaped like a continental cup.</returns>
    internal static bool IsContinentalCup(GetCompetitionsResult competition)
    {
        return competition.Order == 0
            && !competition.IsDomestic
            && competition.Format == GroupAndKnockoutFormat
            && competition.MaxAgeAllowed is null;
    }
}
