using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Finds the national competition, the World Cup, among a league's competitions. Every World Cup step needs the same
/// competition, so the shape that identifies it lives here once.
/// </summary>
internal static class NationalCompetitionFinder
{
    /// <summary>
    /// Picks the national competitions out of a league's competitions.
    /// </summary>
    /// <param name="competitions">The league's competitions.</param>
    /// <returns>The competitions shaped like a national competition, ordered by competition id.</returns>
    internal static IReadOnlyList<GetCompetitionsResult> Find(IReadOnlyList<GetCompetitionsResult> competitions)
    {
        return competitions
            .Where(IsNationalCompetition)
            .OrderBy(competition => competition.Id)
            .ToList();
    }

    /// <summary>
    /// Tells whether a competition has the shape of a league's national competition, the World Cup: it is not a
    /// division, it is national and it has no age limit.
    /// </summary>
    /// <param name="competition">The competition.</param>
    /// <returns>True when the competition is shaped like a national competition.</returns>
    private static bool IsNationalCompetition(GetCompetitionsResult competition)
    {
        return competition.Order == 0
            && competition.IsNational
            && competition.MaxAgeAllowed is null;
    }
}
